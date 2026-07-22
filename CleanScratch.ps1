<#
.SYNOPSIS
    Deletes probe/screenshot artifacts from .scratch/, keeping backups by default.

.DESCRIPTION
    .scratch/ is where every temporary artifact lands (CLAUDE.md: probe images,
    --screenshot= captures, debug dumps, throwaway scripts). It is git-ignored and
    grows without bound -- 322 files / 206 MB by 2026-07-22 -- so it needs an
    occasional sweep.

    NOT everything in there is disposable. Backup artifacts (*.bundle from
    `git bundle`, *.worktree-backup) are safety nets deliberately parked in a
    git-ignored folder, and one of them is 71 MB of pre-history-rewrite repo
    state. Those are preserved unless you pass -IncludeBackups.

    Empty directories left behind are removed. Nothing outside .scratch/ is ever
    touched, and the folder itself is kept.

    The script supports -WhatIf and -Confirm. Without -Force it prints the plan
    and asks once before deleting.

.PARAMETER OlderThanDays
    Only delete files last written more than N days ago. Default 0 (everything).

.PARAMETER Keep
    Wildcard patterns matched against each file's name; matches are preserved.
    Repeatable, e.g. -Keep '*.md','golden_*.png'.

.PARAMETER IncludeBackups
    Also delete *.bundle / *.worktree-backup files, which are protected by default.

.PARAMETER Force
    Skip the confirmation prompt.

.EXAMPLE
    .\CleanScratch.ps1 -WhatIf
    List exactly what would be deleted, delete nothing.

.EXAMPLE
    .\CleanScratch.ps1
    Sweep every probe artifact, keep the backups, ask once first.

.EXAMPLE
    .\CleanScratch.ps1 -OlderThanDays 7 -Force
    Unattended weekly sweep: drop artifacts older than a week, no prompt.

.EXAMPLE
    .\CleanScratch.ps1 -Keep 'reference_*.png' -Force
    Sweep everything except probe captures you are still comparing against.
#>

[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High')]
param(
    [int]      $OlderThanDays = 0,
    [string[]] $Keep = @(),
    [switch]   $IncludeBackups,
    [switch]   $Force
)

$ErrorActionPreference = "Stop"

$ScratchDir = Join-Path $PSScriptRoot ".scratch"

if (-not (Test-Path $ScratchDir)) {
    Write-Host "Nothing to do: $ScratchDir does not exist." -ForegroundColor Green
    exit 0
}

# Backups parked in .scratch/ are safety nets, not probe output. Protected unless asked for.
$BackupPatterns = @('*.bundle', '*.worktree-backup')

$cutoff = if ($OlderThanDays -gt 0) { (Get-Date).AddDays(-$OlderThanDays) } else { $null }

$all      = @(Get-ChildItem $ScratchDir -Recurse -File -Force)
$doomed   = [System.Collections.Generic.List[object]]::new()
$spared   = [System.Collections.Generic.List[object]]::new()

foreach ($f in $all) {
    $reason = $null

    if (-not $IncludeBackups) {
        foreach ($p in $BackupPatterns) {
            if ($f.Name -like $p) { $reason = "backup"; break }
        }
    }
    if (-not $reason) {
        foreach ($p in $Keep) {
            if ($f.Name -like $p) { $reason = "-Keep '$p'"; break }
        }
    }
    if (-not $reason -and $cutoff -and $f.LastWriteTime -ge $cutoff) {
        $reason = "newer than $OlderThanDays d"
    }

    if ($reason) {
        $spared.Add([pscustomobject]@{ File = $f; Reason = $reason })
    } else {
        $doomed.Add($f)
    }
}

function Format-Size([long]$bytes) {
    if ($bytes -ge 1MB) { return "{0:N1} MB" -f ($bytes / 1MB) }
    if ($bytes -ge 1KB) { return "{0:N0} KB" -f ($bytes / 1KB) }
    return "$bytes B"
}

$doomedBytes = ($doomed | Measure-Object Length -Sum).Sum
if (-not $doomedBytes) { $doomedBytes = 0 }

Write-Host ""
Write-Host ".scratch: $($all.Count) file(s), $(Format-Size (($all | Measure-Object Length -Sum).Sum))" -ForegroundColor Cyan

if ($spared.Count -gt 0) {
    Write-Host "Keeping $($spared.Count):" -ForegroundColor Green
    foreach ($s in $spared | Sort-Object { $_.File.Length } -Descending | Select-Object -First 10) {
        $rel = $s.File.FullName.Substring($ScratchDir.Length + 1)
        Write-Host ("  {0,-10} {1}  ({2})" -f (Format-Size $s.File.Length), $rel, $s.Reason)
    }
    if ($spared.Count -gt 10) { Write-Host "  ... and $($spared.Count - 10) more" }
}

if ($doomed.Count -eq 0) {
    Write-Host "Nothing to delete." -ForegroundColor Green
    exit 0
}

Write-Host "Deleting $($doomed.Count), freeing $(Format-Size $doomedBytes)." -ForegroundColor Yellow

if (-not $Force -and -not $WhatIfPreference) {
    $answer = $Host.UI.PromptForChoice(
        "Clean .scratch",
        "Delete $($doomed.Count) file(s), freeing $(Format-Size $doomedBytes)?",
        @("&Yes", "&No"), 1)
    if ($answer -ne 0) {
        Write-Host "Aborted -- nothing deleted." -ForegroundColor Yellow
        exit 1
    }
}

$deleted = 0
foreach ($f in $doomed) {
    if ($PSCmdlet.ShouldProcess($f.FullName, "Delete")) {
        try {
            Remove-Item -LiteralPath $f.FullName -Force -Confirm:$false
            $deleted++
        } catch {
            Write-Warning "Could not delete $($f.FullName): $($_.Exception.Message)"
        }
    }
}

# Drop directories emptied by the sweep, deepest first so parents collapse too.
$prunedDirs = 0
if (-not $WhatIfPreference) {
    foreach ($d in Get-ChildItem $ScratchDir -Recurse -Directory -Force |
                   Sort-Object { $_.FullName.Length } -Descending) {
        if (-not (Get-ChildItem $d.FullName -Force)) {
            Remove-Item -LiteralPath $d.FullName -Force -Confirm:$false
            $prunedDirs++
        }
    }
}

Write-Host ""
Write-Host "Deleted $deleted file(s), $(Format-Size $doomedBytes) freed; $prunedDirs empty dir(s) pruned." -ForegroundColor Green
