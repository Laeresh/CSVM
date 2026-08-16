<#
.SYNOPSIS
    Runs the 5-versus-5 AI dogfight N times and tallies the mid-air collisions.

.DESCRIPTION
    The instrument behind analysis/ai-midair-collisions/FINDINGS.md: an A/B rig for any change
    to the AI's collision behaviour. Each run is a spectated Instant Action squadron dogfight
    (dogfight-5v5.json next to this script) with every human pinned inert, bounded by --frames
    and ended by saving its screenshot, which is also the completion signal.

    It counts three things out of the run's stdout log:
      midair      "midair aspect" lines, printed by FlightController.Crash when the struck body
                  is another aircraft. FATAL collisions only (see FINDINGS.md, ASSUMED-2).
      head-on     those whose two velocity vectors are at least HeadOnDeg apart.
      arms        avoid-crash mode entries, and how many named an aircraft rather than terrain.

    Read FINDINGS.md before trusting a single run: --det does NOT pin this scenario across
    processes, so each run is a sample and only a multi-run mean means anything.

.EXAMPLE
    .\analysis\ai-midair-collisions\dogfight-trials.ps1 -Runs 8 -Label break-right
    Eight runs, tallied, with the per-run rows kept under that label.
#>
param(
    [int]$Runs = 8,
    [int]$Frames = 9000,
    [string]$Label = "run",
    [int]$HeadOnDeg = 135,
    [int]$TimeoutMinutes = 6
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot  = Split-Path -Parent (Split-Path -Parent $ScriptDir)
$IaFile    = Join-Path $ScriptDir "dogfight-5v5.json"
$ShotDir   = Join-Path $RepoRoot ".scratch\ai-midair"

if (-not (Test-Path $ShotDir)) { New-Item -ItemType Directory -Force $ShotDir | Out-Null }
$rows = @()

Push-Location $RepoRoot
try {
    for ($i = 1; $i -le $Runs; $i++) {
        $png = Join-Path $ShotDir "$Label-$i.png"
        if (Test-Path $png) { Remove-Item $png }
        $before = Get-Date

        # RunGame.ps1 launches a GUI-subsystem Godot, so it returns immediately and the run is
        # watched through its artefacts rather than waited on.
        & (Join-Path $RepoRoot "RunGame.ps1") --chapter=C1 "--ia=$IaFile" --ai-attack=5 `
            --debug-spectate --det --no-vsync "--screenshot=$png" "--frames=$Frames" | Out-Null

        $deadline = (Get-Date).AddMinutes($TimeoutMinutes)
        while ((Get-Date) -lt $deadline -and -not (Test-Path $png)) { Start-Sleep -Seconds 3 }
        if (-not (Test-Path $png)) {
            # A run that never saved its shot did not reach $Frames, so its counts would be a
            # short sample masquerading as a full one. Dropped, and said so.
            Write-Host ("run {0}: TIMED OUT, excluded" -f $i)
            continue
        }

        $out = Get-ChildItem (Join-Path $RepoRoot ".scratch\logs") -Filter *.out |
            Where-Object { $_.LastWriteTime -gt $before } |
            Sort-Object LastWriteTime -Descending | Select-Object -First 1
        $lines  = Get-Content $out.FullName
        $midair = @($lines | Select-String 'midair aspect')
        $headOn = @($midair | Where-Object {
            $m = [regex]::Match($_.Line, 'tracks (\d+)')
            $m.Success -and [int]$m.Groups[1].Value -ge $HeadOnDeg
        })
        $arms       = @($lines | Select-String '\-> avoid crash')
        $onAircraft = @($arms | Where-Object { $_.Line -match '/airframe\)' })

        $rows += [pscustomobject]@{
            Run = $i; Midair = $midair.Count; HeadOn = $headOn.Count
            Arms = $arms.Count; ArmsOnAircraft = $onAircraft.Count; Log = $out.Name
        }
        Write-Host ("run {0}: midair={1} (head-on {2}), arms={3} of which on aircraft {4}" -f `
            $i, $midair.Count, $headOn.Count, $arms.Count, $onAircraft.Count)
    }
}
finally { Pop-Location }

if ($rows.Count -eq 0) { Write-Host "no run completed"; exit 1 }

Write-Host ""
Write-Host "=== $Label over $($rows.Count) completed run(s) of $Frames frames ==="
$rows | Format-Table -AutoSize
$mean = ($rows | Measure-Object Midair -Average).Average
$sd = if ($rows.Count -gt 1) {
    [Math]::Sqrt((($rows | ForEach-Object { [Math]::Pow($_.Midair - $mean, 2) } |
        Measure-Object -Sum).Sum) / ($rows.Count - 1))
} else { 0 }
Write-Host ("mid-airs per run: mean {0:N2}, sd {1:N2}, n {2}" -f $mean, $sd, $rows.Count)
Write-Host ("head-on per run : {0:N2}" -f ($rows | Measure-Object HeadOn -Average).Average)
Write-Host ("arms per run    : {0:N1}, on an aircraft {1:N1}" -f `
    ($rows | Measure-Object Arms -Average).Average,
    ($rows | Measure-Object ArmsOnAircraft -Average).Average)
