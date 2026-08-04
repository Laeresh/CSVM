# The 8-chapter --debug-anim sweep this item is measured with (BL-135 / PLAN-m3-polish-6 B12).
#
# One deterministic `--freecam --det --debug-anim` probe per chapter, terminated by a
# `--screenshot` at frame 120 (verification SHELL-12: --frames alone is not an exit condition),
# with each run's console output normalized into a diffable file. Run it once before the change
# and once after, then `git diff --no-index` the two directories: everything that survives
# normalization is behaviour — bind censuses, condition verdicts, live motion/puffer counts,
# the per-node debug poses, and the screenshot's pixel MD5.
#
# Normalization strips only what is not behaviour: wall-clock timings, the [perf] line, memory
# figures and absolute paths.
#
#   .\analysis\bl-135-callsequence-lag\sweep.ps1 -Label baseline
#   .\analysis\bl-135-callsequence-lag\sweep.ps1 -Label drained
#   git diff --no-index .scratch\bl-135\baseline .scratch\bl-135\drained
#
# Read-only against the install; writes to .scratch/ only.

[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Label,
    [string[]]$Chapters = @('C1', 'C1B', 'C1C', 'C2', 'C2B', 'C3', 'C4', 'C5'),
    [string]$Out = (Join-Path (Split-Path $PSScriptRoot -Parent | Split-Path -Parent) '.scratch\bl-135')
)

$ErrorActionPreference = 'Stop'
$Root = Split-Path $PSScriptRoot -Parent | Split-Path -Parent
$dir = Join-Path $Out $Label
New-Item -ItemType Directory -Force $dir | Out-Null

foreach ($ch in $Chapters) {
    $png = Join-Path $dir "$ch.png"
    $before = Get-ChildItem (Join-Path $Root '.scratch\logs') -Filter 'probe-*.out' -ErrorAction SilentlyContinue
    & (Join-Path $Root 'RunProbe.ps1') --freecam "--chapter=$ch" --debug-anim --det "--screenshot=$png" --frames=120 |
        Out-Null
    $code = $LASTEXITCODE
    $log = Get-ChildItem (Join-Path $Root '.scratch\logs') -Filter 'probe-*.out' |
        Where-Object { $_.FullName -notin ($before | ForEach-Object FullName) } |
        Sort-Object LastWriteTime | Select-Object -Last 1

    $text = if ($log) { Get-Content $log.FullName } else { @() }
    $norm = $text |
        Where-Object { $_ -notmatch '^\[perf\]' } |
        ForEach-Object {
            $l = $_
            $l = $l -replace [regex]::Escape($Root), '<root>'
            $l = $l -replace '\d+(\,\d+)? ?ms\b', '<ms>'
            $l = $l -replace '\b\d+(\.\d+)? ?MiB\b', '<mem>'
            $l = $l -replace 'sim_time=\d+(\,\d+)?', 'sim_time=<t>'
            $l
        }
    @("exit=$code") + $norm | Set-Content (Join-Path $dir "$ch.log") -Encoding utf8
    Write-Host ("  {0,-4} exit {1}  {2} line(s)" -f $ch, $code, @($norm).Count)
}

Write-Host ""
Write-Host "wrote $dir"
