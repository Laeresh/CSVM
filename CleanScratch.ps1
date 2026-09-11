<#
.SYNOPSIS
    Sweeps the workspace's throwaway state: probe artifacts in .scratch/ and
    finished agent worktrees in .claude/worktrees/.

.DESCRIPTION
    .scratch/ is where every temporary artifact lands (PROJECT_CONTEXT.md: probe images,
    --screenshot= captures, debug dumps, throwaway scripts). It is git-ignored and
    grows without bound -- 322 files / 206 MB by 2026-07-22 -- so it needs an
    occasional sweep.

    NOT everything in there is disposable. Backup artifacts (*.bundle from
    `git bundle`, *.worktree-backup) are safety nets deliberately parked in a
    git-ignored folder, and one of them is 71 MB of pre-history-rewrite repo
    state. Those are preserved unless you pass -IncludeBackups.

    Empty directories left behind are removed. Nothing outside .scratch/ is ever
    touched, and the folder itself is kept.

    JUNCTIONS (added 2026-08-05). Windows PowerShell 5.1's recursive enumeration
    and deletion FOLLOW directory reparse points (junctions, symlinks), so a
    junction parked in .scratch/ or a worktree -- e.g. one an agent made to
    OriginalScreenshots\ -- would get its TARGET's files deleted by a naive
    sweep. This script never recurses through a reparse point: it unlinks the
    link itself (target untouched) and reports having done so. All enumeration
    below goes through Get-SweepInventory / Get-ReparseDirectory, which stop at
    reparse points by construction.

    WORKTREES (added 2026-07-22). Subagents run in git worktrees under
    .claude/worktrees/. They are git-ignored, so they survive every sweep and
    accumulate -- ten of them after one plan, each a full checkout that adds
    false hits to every repo-wide grep. This script removes the finished ones and
    prunes the stale metadata git keeps after a worktree directory is deleted by
    hand (`git worktree list` reports those as "prunable").

    UNIT-TEST SCRATCH. The xUnit suite builds throwaway input under
    %TEMP%\csvm-tests\run-<pid>-<id>, and the test host deletes its own run root
    as it exits. This script is the backstop for the roots a killed or crashed
    run leaves behind, and for the flat pile older builds left before the run
    roots existed. A root whose process is still alive is never touched: the
    whole folder is spared while any test run is going, because deleting it
    would pull the input out from under a suite running in another window.

    The folder is renamed aside and deleted by a detached `rd /s /q`, not
    deleted in place. An accumulation here reaches six figures of directories,
    and a recursive delete of one takes far longer than the rename does.

    Worktree safety rules, in order:
      * Only worktrees under .claude/worktrees/ are ever considered. The main
        worktree and the one you are standing in are never touched.
      * A worktree with uncommitted changes is SPARED and reported, unless you
        pass -IncludeDirtyWorktrees. Deleting one destroys work that exists
        nowhere else.
      * Removing a worktree does NOT delete its branch, so committed work always
        survives. Leftover branches are reported; -PruneBranches deletes the ones
        already merged into main, using `git branch -d`, which refuses unmerged
        branches by design.

.PARAMETER OlderThanDays
    Only delete files last written more than N days ago. Default 0 (everything).
    Applies to .scratch/ files only, not to worktrees.

.PARAMETER Keep
    Wildcard patterns matched against each file's name; matches are preserved.
    Repeatable, e.g. -Keep '*.md','golden_*.png'.

.PARAMETER IncludeBackups
    Also delete *.bundle / *.worktree-backup files, which are protected by default.

.PARAMETER SkipWorktrees
    Leave .claude/worktrees/ alone entirely; sweep only .scratch/.

.PARAMETER SkipTestTemp
    Leave %TEMP%\csvm-tests alone. The sweep already stands down on its own while
    a test run owns a root in there, so this is for the case where you want the
    abandoned roots kept, e.g. to read a failed run's fixtures.

.PARAMETER IncludeDirtyWorktrees
    Also remove worktrees with uncommitted changes. Destroys uncommitted work.

.PARAMETER PruneBranches
    Delete every local branch already merged into main, except main itself, the
    branch you are on, and any branch checked out in a worktree being kept.
    Unmerged branches are always kept -- `git branch -d` refuses them.

    This is deliberately NOT limited to branches removed in the same run. Agent
    worktrees leave their branches behind, and those outlive the worktree by any
    number of sweeps; scoping the prune to "worktrees removed just now" made the
    switch silently do nothing whenever the directories had already been cleaned
    up, which is the common case.

.PARAMETER Force
    Skip the confirmation prompt.

.EXAMPLE
    .\CleanScratch.ps1 -WhatIf
    List exactly what would be deleted, delete nothing.

.EXAMPLE
    .\CleanScratch.ps1
    Sweep probe artifacts and finished worktrees, keep backups, ask once first.

.EXAMPLE
    .\CleanScratch.ps1 -OlderThanDays 7 -Force
    Unattended weekly sweep: drop artifacts older than a week, no prompt.

.EXAMPLE
    .\CleanScratch.ps1 -PruneBranches -Force
    Full tidy after a finished plan: artifacts, worktrees, and the merged
    branches they leave behind.

.EXAMPLE
    .\CleanScratch.ps1 -SkipWorktrees -Keep 'reference_*.png' -Force
    Sweep only .scratch/, except probe captures you are still comparing against.
#>

# ConfirmImpact is deliberately Medium, not High. At High, ShouldProcess prompts
# per item on top of this script's own single PromptForChoice -- so a 132-file
# sweep asked twice, the second time item by item. -WhatIf and -Confirm still work.
[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]
param(
    [int]      $OlderThanDays = 0,
    [string[]] $Keep = @(),
    [switch]   $IncludeBackups,
    [switch]   $SkipWorktrees,
    [switch]   $SkipTestTemp,
    [switch]   $IncludeDirtyWorktrees,
    [switch]   $PruneBranches,
    [switch]   $Force
)

$ErrorActionPreference = "Stop"

$ScratchDir   = Join-Path $PSScriptRoot ".scratch"
$WorktreeRoot = Join-Path $PSScriptRoot ".claude\worktrees"
$TestTempRoot = Join-Path ([System.IO.Path]::GetTempPath()) "csvm-tests"

# Backups parked in .scratch/ are safety nets, not probe output. Protected unless asked for.
$BackupPatterns = @('*.bundle', '*.worktree-backup')

$cutoff = if ($OlderThanDays -gt 0) { (Get-Date).AddDays(-$OlderThanDays) } else { $null }

# Enumerates directory reparse points (junctions/symlinks) under $Root without
# ever descending into one -- PS 5.1's -Recurse would, which is the whole bug.
function Get-ReparseDirectory([string]$Root) {
    $found = [System.Collections.Generic.List[object]]::new()
    if (-not (Test-Path -LiteralPath $Root)) { return , $found }
    $stack = [System.Collections.Generic.Stack[string]]::new()
    $stack.Push($Root)
    while ($stack.Count -gt 0) {
        foreach ($child in Get-ChildItem -LiteralPath $stack.Pop() -Directory -Force) {
            if ($child.Attributes -band [System.IO.FileAttributes]::ReparsePoint) {
                $found.Add($child)
            } else {
                $stack.Push($child.FullName)
            }
        }
    }
    return , $found
}

# Walks $Root collecting real files and directory reparse points, stopping at
# every reparse point instead of recursing through it. File symlinks are listed
# as files: Remove-Item on one deletes only the link, never the target.
function Get-SweepInventory([string]$Root) {
    $files     = [System.Collections.Generic.List[object]]::new()
    $junctions = [System.Collections.Generic.List[object]]::new()
    $dirs      = [System.Collections.Generic.List[object]]::new()
    if (Test-Path -LiteralPath $Root) {
        $stack = [System.Collections.Generic.Stack[string]]::new()
        $stack.Push($Root)
        while ($stack.Count -gt 0) {
            foreach ($child in Get-ChildItem -LiteralPath $stack.Pop() -Force) {
                if ($child.PSIsContainer) {
                    if ($child.Attributes -band [System.IO.FileAttributes]::ReparsePoint) {
                        $junctions.Add($child)
                    } else {
                        $dirs.Add($child)
                        $stack.Push($child.FullName)
                    }
                } else {
                    $files.Add($child)
                }
            }
        }
    }
    return [pscustomobject]@{ Files = $files; Junctions = $junctions; Dirs = $dirs }
}

# Deletes the link itself; the target directory and its contents are untouched.
function Remove-ReparseLink([string]$Path) {
    [System.IO.Directory]::Delete($Path)
}

# What is sitting in the unit suite's temp scratch, and whether anyone still
# needs it. Counting is capped: the number is only ever printed, and a pile left
# by builds from before the run roots existed reaches six figures. The liveness
# probe is separate and cheap -- the kernel filters the enumeration down to
# run-* names, of which there are only ever one per concurrent test host.
function Get-TestTempState([string]$Root) {
    $state = [pscustomobject]@{ Present = $false; Count = 0; Capped = $false; Live = 0 }
    if (-not (Test-Path -LiteralPath $Root)) { return $state }
    $state.Present = $true

    foreach ($entry in [System.IO.Directory]::EnumerateFileSystemEntries($Root)) {
        $state.Count++
        if ($state.Count -ge 2000) { $state.Capped = $true; break }
    }

    foreach ($dir in [System.IO.Directory]::EnumerateDirectories($Root, 'run-*')) {
        if ((Split-Path $dir -Leaf) -notmatch '^run-(\d+)-') { continue }
        $running = $null
        try { $running = Get-Process -Id ([int]$Matches[1]) -ErrorAction Stop } catch { $running = $null }
        if ($running) { $state.Live++ }
    }

    return $state
}

$inventory = Get-SweepInventory $ScratchDir
$all       = @($inventory.Files)
$junctions = $inventory.Junctions
$doomed    = [System.Collections.Generic.List[object]]::new()
$spared    = [System.Collections.Generic.List[object]]::new()

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

# --- Agent worktrees -------------------------------------------------------
# Parsed from `git worktree list --porcelain` rather than by listing the folder,
# because the two disagree in exactly the case that matters: a worktree whose
# directory was deleted by hand still has git metadata and shows as "prunable".
# Listing the folder would miss those entirely.
$wtDoomed  = [System.Collections.Generic.List[object]]::new()
$wtSpared  = [System.Collections.Generic.List[object]]::new()
$isGitRepo = $false

if (-not $SkipWorktrees) {
    $null = & git -C $PSScriptRoot rev-parse --git-dir 2>$null
    $isGitRepo = ($LASTEXITCODE -eq 0)
}

if ($isGitRepo) {
    $porcelain = @(& git -C $PSScriptRoot worktree list --porcelain)
    $wtAll     = [System.Collections.Generic.List[object]]::new()
    $current   = $null
    foreach ($line in $porcelain) {
        if ($line -like 'worktree *') {
            if ($current) { $wtAll.Add($current) }
            $current = [pscustomobject]@{
                Path = $line.Substring(9); Branch = $null; Prunable = $false
            }
        } elseif ($line -like 'branch *') {
            if ($current) { $current.Branch = $line.Substring(7) -replace '^refs/heads/', '' }
        } elseif ($line -like 'prunable*') {
            if ($current) { $current.Prunable = $true }
        }
    }
    if ($current) { $wtAll.Add($current) }

    foreach ($w in $wtAll) {
        $full = $null
        try { $full = [System.IO.Path]::GetFullPath($w.Path) } catch { $full = $w.Path }

        # Only ever the agent worktrees. Never the main checkout, never the one
        # this script is running from.
        if ($full -notlike "$WorktreeRoot*") { continue }
        if ($full -eq [System.IO.Path]::GetFullPath($PSScriptRoot)) { continue }

        $exists = Test-Path $w.Path
        $dirty  = $false
        if ($exists) {
            $status = & git -C $w.Path status --porcelain
            $dirty  = [bool]$status
        }

        # Junctions inside the worktree are unlinked before removal so no
        # recursive delete -- git's or anyone's -- can reach their targets.
        $wtJunctions = if ($exists) { Get-ReparseDirectory $w.Path } else { @() }

        if ($dirty -and -not $IncludeDirtyWorktrees) {
            $wtSpared.Add([pscustomobject]@{ WT = $w; Reason = "uncommitted changes" })
        } else {
            $wtDoomed.Add([pscustomobject]@{
                WT = $w; Exists = $exists; Dirty = $dirty; Junctions = $wtJunctions
            })
        }
    }
}

# --- Branches left behind by worktrees -------------------------------------
# Enumerated from the branch list, NOT from the worktrees removed in this run:
# a branch outlives its worktree by any number of sweeps, so scoping it to
# "removed just now" made -PruneBranches silently no-op once the directories had
# already been cleaned up by hand.
#
# Safe by construction: `git branch -d` refuses to delete anything not merged, so
# a branch carrying unique commits survives regardless of what is listed here.
$brDoomed = [System.Collections.Generic.List[string]]::new()

if ($PruneBranches -and $isGitRepo) {
    # Branches held by worktrees we are KEEPING cannot be deleted; ones held by
    # worktrees being removed become deletable, so they stay in the list.
    $heldByKept = @{}
    foreach ($s in $wtSpared) {
        if ($s.WT.Branch) { $heldByKept[$s.WT.Branch] = $true }
    }

    $currentBranch = (& git -C $PSScriptRoot rev-parse --abbrev-ref HEAD)
    foreach ($line in @(& git -C $PSScriptRoot branch --merged main)) {
        $b = $line.TrimStart('*', '+', ' ').Trim()
        if (-not $b)                     { continue }
        if ($b -eq 'main')               { continue }
        if ($b -eq $currentBranch)       { continue }
        if ($b -like '(*')               { continue }   # detached HEAD placeholder
        if ($heldByKept.ContainsKey($b)) { continue }
        $brDoomed.Add($b)
    }
}

# --- The unit suite's temp scratch -----------------------------------------
$tempState = if ($SkipTestTemp) {
    [pscustomobject]@{ Present = $false; Count = 0; Capped = $false; Live = 0 }
} else {
    Get-TestTempState $TestTempRoot
}
$tempDoomed = $tempState.Present -and $tempState.Live -eq 0

$doomedBytes = ($doomed | Measure-Object Length -Sum).Sum
if (-not $doomedBytes) { $doomedBytes = 0 }

Write-Host ""
if ($all.Count -gt 0) {
    Write-Host ".scratch: $($all.Count) file(s), $(Format-Size (($all | Measure-Object Length -Sum).Sum))" -ForegroundColor Cyan
} else {
    Write-Host ".scratch: empty or absent" -ForegroundColor Cyan
}

if ($spared.Count -gt 0) {
    Write-Host "Keeping $($spared.Count):" -ForegroundColor Green
    foreach ($s in $spared | Sort-Object { $_.File.Length } -Descending | Select-Object -First 10) {
        $rel = $s.File.FullName.Substring($ScratchDir.Length + 1)
        Write-Host ("  {0,-10} {1}  ({2})" -f (Format-Size $s.File.Length), $rel, $s.Reason)
    }
    if ($spared.Count -gt 10) { Write-Host "  ... and $($spared.Count - 10) more" }
}

if ($wtSpared.Count -gt 0) {
    Write-Host "Keeping $($wtSpared.Count) worktree(s):" -ForegroundColor Green
    foreach ($s in $wtSpared) {
        $name = Split-Path $s.WT.Path -Leaf
        Write-Host ("  {0,-28} {1}  (pass -IncludeDirtyWorktrees to remove)" -f $name, $s.Reason)
    }
}

if ($junctions.Count -gt 0) {
    Write-Host "Junctions in .scratch: unlinking $($junctions.Count) (link only, target untouched)" -ForegroundColor Cyan
    foreach ($j in $junctions) {
        Write-Host ("  {0}  ->  {1}" -f $j.FullName.Substring($ScratchDir.Length + 1), ($j.Target -join ', '))
    }
}

if ($wtDoomed.Count -gt 0) {
    Write-Host "Worktrees: removing $($wtDoomed.Count)" -ForegroundColor Cyan
    foreach ($d in $wtDoomed) {
        $name  = Split-Path $d.WT.Path -Leaf
        $notes = @()
        if (-not $d.Exists) { $notes += "already gone, metadata only" }
        if ($d.Dirty)       { $notes += "DIRTY -- uncommitted work will be lost" }
        if ($d.WT.Branch)   { $notes += "branch $($d.WT.Branch)" }
        if ($d.Junctions.Count -gt 0) {
            $notes += "$($d.Junctions.Count) junction(s) unlinked first, targets untouched"
        }
        Write-Host ("  {0,-28} {1}" -f $name, ($notes -join "; "))
    }
}

if ($brDoomed.Count -gt 0) {
    Write-Host "Branches: deleting $($brDoomed.Count) merged into main" -ForegroundColor Cyan
    foreach ($b in $brDoomed) { Write-Host "  $b" }
}

if ($tempState.Present -and $tempState.Live -gt 0) {
    Write-Host "Test scratch: keeping it -- $($tempState.Live) test run(s) are still using it" -ForegroundColor Green
} elseif ($tempDoomed) {
    $tempCount = if ($tempState.Capped) { "$($tempState.Count)+" } else { "$($tempState.Count)" }
    Write-Host "Test scratch: removing $TestTempRoot ($tempCount entries)" -ForegroundColor Cyan
}

if ($doomed.Count -eq 0 -and $wtDoomed.Count -eq 0 -and $brDoomed.Count -eq 0 -and
    $junctions.Count -eq 0 -and -not $tempDoomed) {
    Write-Host "Nothing to delete." -ForegroundColor Green
    exit 0
}

if ($doomed.Count -gt 0) {
    Write-Host "Deleting $($doomed.Count) file(s), freeing $(Format-Size $doomedBytes)." -ForegroundColor Yellow
}

if (-not $Force -and -not $WhatIfPreference) {
    $what = @()
    if ($doomed.Count -gt 0)    { $what += "$($doomed.Count) file(s), $(Format-Size $doomedBytes)" }
    if ($junctions.Count -gt 0) { $what += "$($junctions.Count) junction link(s)" }
    if ($wtDoomed.Count -gt 0)  { $what += "$($wtDoomed.Count) worktree(s)" }
    if ($brDoomed.Count -gt 0)  { $what += "$($brDoomed.Count) branch(es)" }
    if ($tempDoomed)            { $what += "the unit suite's temp scratch" }
    # A non-interactive host (scheduled task, CI, an agent shell) cannot prompt and
    # throws here. Fail CLOSED with an actionable message rather than a raw .NET
    # exception -- deleting on the grounds that nobody could be asked is the wrong
    # default for a script whose whole job is deletion.
    $answer = 1
    try {
        $answer = $Host.UI.PromptForChoice(
            "Clean workspace",
            "Delete $($what -join ' and ')?",
            @("&Yes", "&No"), 1)
    } catch {
        Write-Host "Cannot prompt in a non-interactive host -- nothing deleted." -ForegroundColor Yellow
        Write-Host "Re-run with -Force to sweep unattended, or -WhatIf to see the plan." -ForegroundColor Yellow
        exit 1
    }
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

# Unlink junctions parked in .scratch -- the link only, never the target.
$junctionsUnlinked = 0
foreach ($j in $junctions) {
    if ($PSCmdlet.ShouldProcess($j.FullName, "Unlink junction (target untouched)")) {
        try {
            Remove-ReparseLink $j.FullName
            $junctionsUnlinked++
        } catch {
            Write-Warning "Could not unlink $($j.FullName): $($_.Exception.Message)"
        }
    }
}

# Renamed aside and handed to a detached `rd`, never deleted in place: see the
# unit-test scratch note in the header. A rename fails while any process holds a
# handle below the folder, which is one more guard on a live test run.
$tempSwept = $false
if ($tempDoomed -and $PSCmdlet.ShouldProcess($TestTempRoot, "Remove the unit suite's temp scratch")) {
    $aside = "$TestTempRoot.sweep-" + [guid]::NewGuid().ToString('N')
    try {
        [System.IO.Directory]::Move($TestTempRoot, $aside)
        $null = Start-Process -FilePath $env:ComSpec `
            -ArgumentList '/c', 'rd', '/s', '/q', "`"$aside`"" -WindowStyle Hidden
        $tempSwept = $true
    } catch {
        Write-Warning "Could not sweep ${TestTempRoot}: $($_.Exception.Message)"
    }
}

# Drop directories emptied by the sweep, deepest first so parents collapse too.
# $inventory.Dirs was gathered without descending into reparse points; the
# emptiness probe below is non-recursive, so it cannot reach through one either.
$prunedDirs = 0
if (-not $WhatIfPreference) {
    foreach ($d in $inventory.Dirs | Sort-Object { $_.FullName.Length } -Descending) {
        if (-not (Test-Path -LiteralPath $d.FullName)) { continue }
        if (-not (Get-ChildItem -LiteralPath $d.FullName -Force)) {
            Remove-Item -LiteralPath $d.FullName -Force -Confirm:$false
            $prunedDirs++
        }
    }
}

# --- Remove the worktrees --------------------------------------------------
# `git worktree remove` is used rather than Remove-Item so git's own metadata is
# updated in the same step; a directory already deleted by hand is left to the
# `prune` below, which is exactly what clears a "prunable" entry.
$wtRemoved  = 0
$wtBranches = [System.Collections.Generic.List[string]]::new()

foreach ($d in $wtDoomed) {
    $name = Split-Path $d.WT.Path -Leaf
    if (-not $PSCmdlet.ShouldProcess($d.WT.Path, "Remove worktree")) { continue }
    if ($d.Exists) {
        # Unlink any junctions first so the removal below cannot follow one
        # into its target. Skipped on failure: better a leftover worktree than
        # a recursive delete racing an intact junction.
        $unlinkFailed = $false
        foreach ($j in $d.Junctions) {
            try {
                Remove-ReparseLink $j.FullName
                $junctionsUnlinked++
            } catch {
                Write-Warning "Could not unlink $($j.FullName): $($_.Exception.Message)"
                $unlinkFailed = $true
            }
        }
        if ($unlinkFailed) {
            Write-Warning "Skipping worktree ${name}: junction(s) still in place."
            continue
        }
        $args = @('-C', $PSScriptRoot, 'worktree', 'remove', $d.WT.Path)
        if ($d.Dirty) { $args += '--force' }
        $out = & git @args
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "Could not remove worktree ${name}: $out"
            continue
        }
    }
    $wtRemoved++
    if ($d.WT.Branch) { $wtBranches.Add($d.WT.Branch) }
}

if ($isGitRepo -and -not $WhatIfPreference) {
    $null = & git -C $PSScriptRoot worktree prune
}

# --- Delete the merged branches --------------------------------------------
# Runs AFTER worktree removal so a branch that was checked out a moment ago is
# now free. `git branch -d` refuses anything unmerged, so this cannot lose work.
$brDeleted = 0
$brKept    = [System.Collections.Generic.List[string]]::new()

foreach ($b in $brDoomed) {
    $null = & git -C $PSScriptRoot rev-parse --verify --quiet "refs/heads/$b"
    if ($LASTEXITCODE -ne 0) { continue }
    if (-not $PSCmdlet.ShouldProcess($b, "Delete branch")) { continue }
    $out = & git -C $PSScriptRoot branch -d $b
    if ($LASTEXITCODE -eq 0) {
        $brDeleted++
    } else {
        $brKept.Add($b)
        Write-Warning "Could not delete branch ${b}: $out"
    }
}

# Branches left behind by worktrees removed this run, when -PruneBranches was not
# asked for -- reported so they do not accumulate unnoticed.
if (-not $PruneBranches -and $wtBranches.Count -gt 0) {
    foreach ($b in $wtBranches) {
        $null = & git -C $PSScriptRoot rev-parse --verify --quiet "refs/heads/$b"
        if ($LASTEXITCODE -eq 0) { $brKept.Add($b) }
    }
}

Write-Host ""
Write-Host "Deleted $deleted file(s), $(Format-Size $doomedBytes) freed; $prunedDirs empty dir(s) pruned." -ForegroundColor Green
if ($junctionsUnlinked -gt 0) {
    Write-Host "Unlinked $junctionsUnlinked junction(s) -- targets untouched." -ForegroundColor Green
}
if ($wtRemoved -gt 0) {
    Write-Host "Removed $wtRemoved worktree(s)." -ForegroundColor Green
}
if ($tempSwept) {
    Write-Host "Swept the unit suite's temp scratch; the delete runs on in the background." -ForegroundColor Green
}
if ($brDeleted -gt 0) {
    Write-Host "Deleted $brDeleted merged branch(es)." -ForegroundColor Green
}
if ($brKept.Count -gt 0) {
    if ($PruneBranches) {
        Write-Host "Kept $($brKept.Count) branch(es) -- not merged into main:" -ForegroundColor Yellow
    } else {
        Write-Host "$($brKept.Count) branch(es) left behind (pass -PruneBranches to drop the merged ones):" -ForegroundColor Yellow
    }
    foreach ($b in $brKept) { Write-Host "  $b" }
}
