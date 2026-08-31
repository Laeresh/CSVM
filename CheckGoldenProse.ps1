#!/usr/bin/env pwsh
# Prose contract for the "exercises" field in analysis/goldens/manifest.json.
#
# That field says what a shot covers TODAY. It is rewritten on a re-pin, never appended to, so an
# item id, a date or an "also exercises" clause in it means somebody treated it as a changelog.
# The history of a shot is git log -p on the manifest.
#
# Pure ASCII on purpose (PROJECT_CONTEXT.md). Files are READ only.
# Exit code 1 on a violation, so a caller can gate a commit on it.
#
# Usage:
#   ./CheckGoldenProse.ps1                  this tree
#   ./CheckGoldenProse.ps1 -Root <path>     another worktree
[CmdletBinding()]
param(
    [string]$Root,
    [switch]$Quiet
)

if (-not $Root) { $Root = $PSScriptRoot }
if (-not $Root) { $Root = (Get-Location).Path }

$cap = 250
$manifest = Join-Path $Root 'analysis/goldens/manifest.json'
if (-not (Test-Path -LiteralPath $manifest -PathType Leaf)) {
    if (-not $Quiet) { Write-Output 'no golden manifest here' }
    exit 0
}

try { $m = [IO.File]::ReadAllText($manifest) | ConvertFrom-Json }
catch {
    if (-not $Quiet) {
        Write-Output 'analysis/goldens/manifest.json does not parse as JSON - fix it before committing.'
    }
    exit 1
}

$bad = @()
foreach ($shot in $m.shots) {
    $e = [string]$shot.exercises
    if ($e.Length -gt $cap) {
        $bad += ('{0}: exercises is {1} chars, cap is {2}' -f $shot.name, $e.Length, $cap)
    }
    if ($e -match '(BL|PT|CAP)-\d+|PLAN-|[Aa]lso exercises|\d{4}-\d{2}-\d{2}') {
        $bad += ('{0}: exercises carries history or an item id -- {1}' -f $shot.name, $Matches[0])
    }
}

if ($bad.Count -eq 0) {
    if (-not $Quiet) { Write-Output 'golden manifest prose within contract' }
    exit 0
}

if (-not $Quiet) {
    foreach ($b in $bad) { Write-Output ('BLOCKED: ' + $b) }
    Write-Output ''
    Write-Output 'analysis/goldens/manifest.json: exercises describes what a shot covers TODAY - one'
    Write-Output 'sentence, under 250 chars, no item ids, no dates, no past-change deltas. REWRITE the'
    Write-Output 'field on a re-pin, never append; the history belongs in the commit message and in'
    Write-Output 'git log -p. See analysis/goldens/README.md.'
}
exit 1
