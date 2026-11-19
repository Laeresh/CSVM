#!/usr/bin/env pwsh
# Lands a worktree branch on main: merge main in, run the complete battery on that tree, merge the
# branch into main. All three steps run inside one process that holds a machine-wide mutex, so
# landings queue instead of racing. Without the queue, two sessions each test "main + my branch",
# the first lands, and the second's green tree is no longer what main would become, so it merges
# again and reruns, and the third does the same after it (the merge cascade).
#
# THE LOCK LIVES IN THIS PROCESS. A session's tool calls are separate processes, so "take a lock,
# then merge, then test" across calls would drop it between them. Only a whole landing inside one
# process keeps main still between the battery and the merge.
#
# A DIRECT COMMIT TO MAIN BYPASSES THE QUEUE. Before merging, the script checks main is still the
# commit it merged in and refuses otherwise (exit 5); a rerun merges the new main and tests again.
#
# A merged tree whose CSVM/, CSVM.Tests/ and analysis/goldens/ equal main's lands without the battery:
# documentation and tooling changes give it nothing to judge (PROJECT_CONTEXT.md exempts them).
#
# A HOLDER WITH LANDINGS QUEUED BEHIND IT RUNS -Quick, not the complete battery, and adds its merge
# to the owed list (csvm-landing-owed.txt in the git common dir). The last landing in the queue runs
# the complete battery on main plus all of them: a pass clears the list, a failure prints it as the
# suspects. Waiters register as csvm-landing-queue/<pid>.txt while they wait for the lock. A
# no-battery branch still runs the complete battery when it is last and the list is not empty.
#
# Exit codes: 0 landed; 1 battery FAIL; 2 refused before anything changed; 3 battery DEFERRED;
# 4 merging main in conflicted (aborted, resolve by hand, commit, rerun); 5 main moved during the run.
# On 1, 3 and 5 the branch keeps its merge of main; nothing reached main.
#
# Pure ASCII on purpose (PROJECT_CONTEXT.md).
#
# Usage (start it with run_in_background; it waits for the queue, then for the battery):
#   .\scripts\Land.ps1 -Worktree <worktree path> -Message "Merge <branch>: <what landed>"
#   .\scripts\Land.ps1 -SelfTest
[CmdletBinding()]
param(
    [string]$Worktree = (Get-Location).Path,
    [string]$Message = "",
    # The battery to run on the merged tree. Defaults to the worktree's own RunTests.ps1.
    [string]$Runner = "",
    [string]$LockName = "Global\csvm-landing",
    [switch]$SelfTest
)

$ErrorActionPreference = "Continue"
. (Join-Path $PSScriptRoot "TreeStampCore.ps1")
$GreenWhat = "CSVM/, CSVM.Tests/ and analysis/goldens/"

function Invoke-Git {
    param([string]$Root)
    $out = & git -C $Root @args 2>&1
    $script:GitExit = $LASTEXITCODE
    return @($out | ForEach-Object { "$_" })
}

function Stop-Land {
    param([int]$Code, [string]$Why)
    $color = if ($Code -eq 0) { "Green" } else { "Red" }
    Write-Host "  Land: $Why" -ForegroundColor $color
    exit $Code
}

function Invoke-SelfTest {
    $exe = (Get-Process -Id $PID).Path
    $root = Join-Path ([IO.Path]::GetTempPath()) ("csvm-land-" + [guid]::NewGuid().ToString("N"))
    $main = Join-Path $root "main"
    $runner = Join-Path $root "runner.ps1"
    $lock = "Local\csvm-land-selftest-" + [guid]::NewGuid().ToString("N")
    $script:failures = 0
    function Assert-Land([bool]$Ok, [string]$What) {
        if ($Ok) { Write-Host "  ok    $What" } else { Write-Host "  FAIL  $What" -ForegroundColor Red; $script:failures++ }
    }
    # $File is relative to the branch's root; the battery's paths are CSVM/ and the like.
    function New-Branch([string]$Name, [string]$File, [string]$Text) {
        $wt = Join-Path $root $Name
        $null = Invoke-Git $main worktree add $wt -b $Name
        Set-Content -LiteralPath (Join-Path $wt $File) -Value $Text
        $null = Invoke-Git $wt add -A
        $null = Invoke-Git $wt commit -m "$Name work"
        return $wt
    }
    function Invoke-Land([string]$Wt, [string]$Mode) {
        $env:LAND_TEST_MODE = $Mode
        $env:LAND_TEST_MAIN = $main
        $null = & $exe -NoProfile -ExecutionPolicy Bypass -File $PSCommandPath -Worktree $Wt -Runner $runner -LockName $lock *>&1
        return $LASTEXITCODE
    }
    try {
        $null = New-Item -ItemType Directory -Path $main -Force
        Set-Content -LiteralPath $runner -Value @(
            'if ($env:LAND_TEST_MODE -eq "move") { git -C $env:LAND_TEST_MAIN commit -q --allow-empty -m direct }'
            'if ($env:LAND_TEST_MODE -eq "fail") { exit 1 }'
            'exit 0'
        )
        $null = Invoke-Git $main init -q -b main
        $null = Invoke-Git $main config user.name selftest
        $null = Invoke-Git $main config user.email selftest@example.invalid
        $null = New-Item -ItemType Directory -Path (Join-Path $main "CSVM") -Force
        Set-Content -LiteralPath (Join-Path $main "CSVM\a.txt") -Value "base"
        $null = Invoke-Git $main add -A
        $null = Invoke-Git $main commit -q -m base

        $wt = New-Branch "pass" "CSVM\b.txt" "pass"
        $code = Invoke-Land $wt "pass"
        $merged = (Invoke-Git $main log -1 --format=%s main) -join ""
        Assert-Land ($code -eq 0 -and $merged -eq "Merge pass" -and (Test-Path (Join-Path $main "CSVM\b.txt"))) "a green branch lands with a merge commit ($code, '$merged')"
        $verify = (Invoke-Git $main log -1 --format=%B main^2) -join " "
        Assert-Land ($verify -like "Verification: pass with main merged in*Complete RunTests.ps1 PASS*") "the branch carries a Verification commit ('$verify')"

        $before = (Invoke-Git $main rev-parse main) -join ""
        $wt = New-Branch "red" "CSVM\c.txt" "red"
        $code = Invoke-Land $wt "fail"
        Assert-Land ($code -eq 1 -and ((Invoke-Git $main rev-parse main) -join "") -eq $before) "a red battery exits 1 and leaves main alone ($code)"

        $wt = New-Branch "docs" "notes.md" "docs"
        $code = Invoke-Land $wt "fail"
        $verify = (Invoke-Git $main log -1 --format=%B main^2) -join " "
        Assert-Land ($code -eq 0 -and $verify -like "*No battery*") "a branch leaving CSVM/ as main has it lands without the battery ($code)"

        $wt = New-Branch "clash" "CSVM\a.txt" "branch side"
        Set-Content -LiteralPath (Join-Path $main "CSVM\a.txt") -Value "main side"
        $null = Invoke-Git $main commit -q -am "main edits a"
        $code = Invoke-Land $wt "pass"
        $gitDir = (Invoke-Git $wt rev-parse --absolute-git-dir) -join ""
        $mergeHead = Test-Path -LiteralPath (Join-Path $gitDir "MERGE_HEAD")
        Assert-Land ($code -eq 4 -and -not $mergeHead) "a conflict exits 4 with the merge aborted ($code)"

        $wt = New-Branch "moved" "CSVM\d.txt" "moved"
        $code = Invoke-Land $wt "move"
        $tip = (Invoke-Git $main log -1 --format=%s main) -join ""
        Assert-Land ($code -eq 5 -and $tip -eq "direct") "main moved under the run exits 5 without merging ($code, '$tip')"

        # A live waiter's ticket (this process) puts the holder on -Quick and the landing on the owed list.
        $queue = Join-Path $main ".git\csvm-landing-queue"
        $owedList = Join-Path $main ".git\csvm-landing-owed.txt"
        $null = New-Item -ItemType Directory -Path $queue -Force
        Set-Content -LiteralPath (Join-Path $queue "$PID.txt") -Value "waiter"
        $wt = New-Branch "quick1" "CSVM\e.txt" "quick1"
        $code = Invoke-Land $wt "pass"
        $verify = (Invoke-Git $main log -1 --format=%B main^2) -join " "
        $owedNow = (Get-Content -LiteralPath $owedList -ErrorAction SilentlyContinue) -join " "
        Assert-Land ($code -eq 0 -and $verify -like "*-Quick PASS*queued behind*" -and $owedNow -like "* quick1") "a holder with a waiter lands on -Quick and owes the battery ($code, '$owedNow')"
        Remove-Item -LiteralPath (Join-Path $queue "$PID.txt") -Force

        $before = (Invoke-Git $main rev-parse main) -join ""
        $wt = New-Branch "docslast" "notes2.md" "docs"
        $code = Invoke-Land $wt "fail"
        Assert-Land ($code -eq 1 -and ((Invoke-Git $main rev-parse main) -join "") -eq $before -and (Test-Path $owedList)) "a no-battery branch landing last with a battery owed runs it ($code)"

        $wt = New-Branch "full" "CSVM\f.txt" "full"
        $code = Invoke-Land $wt "pass"
        $verify = (Invoke-Git $main log -1 --format=%B main^2) -join " "
        Assert-Land ($code -eq 0 -and $verify -like "*Complete RunTests.ps1 PASS*quick1*" -and -not (Test-Path $owedList)) "the last landing's complete battery covers and clears the owed list ($code)"

        $code = Invoke-Land $main "pass"
        Assert-Land ($code -eq 2) "the main checkout itself is refused ($code)"
    } finally {
        Remove-Item Env:LAND_TEST_MODE, Env:LAND_TEST_MAIN -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue
    }
    if ($script:failures -gt 0) { Write-Host "  Land self-test: $($script:failures) FAILED" -ForegroundColor Red; exit 1 }
    Write-Host "  Land self-test: PASS" -ForegroundColor Green
    exit 0
}

if ($SelfTest) { Invoke-SelfTest }

# --- Refusals before anything changes ---------------------------------------------------------------
$wt = (Invoke-Git $Worktree rev-parse --show-toplevel) -join ""
if ($GitExit -ne 0 -or -not $wt) { Stop-Land 2 "$Worktree is not inside a git worktree" }
$branch = (Invoke-Git $wt symbolic-ref --short -q HEAD) -join ""
if (-not $branch -or $branch -eq "main") { Stop-Land 2 "$wt has '$branch' checked out; run Land.ps1 from a worktree branch, not main or a detached HEAD" }
if ((Invoke-Git $wt status --porcelain).Count -gt 0) { Stop-Land 2 "$wt has uncommitted changes; commit them first, or the battery tests a tree that does not land" }

$mainRoot = ""
$current = ""
foreach ($line in (Invoke-Git $wt worktree list --porcelain)) {
    if ($line -like "worktree *") { $current = $line.Substring(9) }
    if ($line -eq "branch refs/heads/main") { $mainRoot = $current }
}
if (-not $mainRoot) { Stop-Land 2 "no worktree has main checked out, so there is nowhere to merge into" }
if ((Invoke-Git $mainRoot status --porcelain --untracked-files=no).Count -gt 0) { Stop-Land 2 "the main checkout $mainRoot has uncommitted changes to tracked files; land those first" }
if (-not $Runner) { $Runner = Join-Path $wt "scripts\RunTests.ps1" }
if (-not $Message) { $Message = "Merge $branch" }

$common = (Invoke-Git $wt rev-parse --git-common-dir) -join ""
if (-not [IO.Path]::IsPathRooted($common)) { $common = Join-Path $wt $common }
$holderFile = Join-Path $common "csvm-landing.json"
$owedFile = Join-Path $common "csvm-landing-owed.txt"
$queueDir = Join-Path $common "csvm-landing-queue"
$ticket = Join-Path $queueDir "$PID.txt"

# The landings waiting for the lock now; a ticket whose process is gone is swept.
# ponytail: a reused PID keeps a dead ticket alive until that process exits, costing one -Quick landing.
function Get-Waiters {
    @(Get-ChildItem -LiteralPath $queueDir -Filter "*.txt" -ErrorAction SilentlyContinue | Where-Object {
        if (Get-Process -Id ([int]$_.BaseName) -ErrorAction SilentlyContinue) { return $true }
        Remove-Item -LiteralPath $_.FullName -Force -ErrorAction SilentlyContinue
        return $false
    })
}

# --- The queue -------------------------------------------------------------------------------------
$mutex = New-Object System.Threading.Mutex($false, $LockName)
$held = $false
try {
    $null = New-Item -ItemType Directory -Path $queueDir -Force
    [IO.File]::WriteAllText($ticket, $branch)
    while (-not $held) {
        try { $held = $mutex.WaitOne(60000) } catch [System.Threading.AbandonedMutexException] { $held = $true }
        if (-not $held) {
            $holder = "unknown"
            try { $holder = [IO.File]::ReadAllText($holderFile) } catch { }
            Write-Host "  Land: waiting for the landing in progress: $holder"
        }
    }
    Remove-Item -LiteralPath $ticket -Force -ErrorAction SilentlyContinue
    [IO.File]::WriteAllText($holderFile, (ConvertTo-Json -Compress -InputObject ([ordered]@{ branch = $branch; worktree = $wt; pid = $PID; since = (Get-Date).ToString("s") })))
    Write-Host "  Land: $branch holds the landing queue"

    # --- Merge main in, pinned by SHA so the check below compares against what was tested ---------
    $mainAt = (Invoke-Git $wt rev-parse main) -join ""
    $out = Invoke-Git $wt merge --no-edit $mainAt
    if ($GitExit -ne 0) {
        $null = Invoke-Git $wt merge --abort
        $out | ForEach-Object { Write-Host "    $_" }
        Stop-Land 4 "merging main ($($mainAt.Substring(0, 9))) into $branch conflicted and was aborted; merge main in by hand, resolve, commit, then rerun Land.ps1"
    }
    if (((Invoke-Git $wt rev-list --count "$mainAt..HEAD") -join "") -eq "0") { Stop-Land 2 "$branch has nothing main does not already have" }

    $head = (Invoke-Git $wt rev-parse --short HEAD) -join ""
    $headKey = Get-TreeKey -Root $wt -Paths $GreenKeyPaths -Commit "HEAD"
    $body = @("Verification: $branch with main merged in ($($mainAt.Substring(0, 9)))", "")
    $owed = @(Get-Content -LiteralPath $owedFile -ErrorAction SilentlyContinue | Where-Object { $_ })
    $waiters = (Get-Waiters).Count
    $ran = ""
    # The battery judges only these paths; a branch that leaves them as main has them (documentation,
    # tooling) adds nothing for it to judge, unless it is last in line with a complete run owed.
    if ($headKey -and $headKey -eq (Get-TreeKey -Root $wt -Paths $GreenKeyPaths -Commit $mainAt) -and ($waiters -gt 0 -or $owed.Count -eq 0)) {
        Write-Host "  Land: $GreenWhat match main; no battery"
        $body += "No battery: the tree at $head holds main's $GreenWhat unchanged."
    } else {
        # --- The battery, as a child so its exit cannot end this script ----------------------------
        $exe = (Get-Process -Id $PID).Path
        if ($waiters -gt 0) {
            $ran = "quick"
            Write-Host "  Land: $waiters landing(s) queued behind; -Quick now, the complete battery is owed to the last"
            & $exe -NoProfile -ExecutionPolicy Bypass -File $Runner -Quick
        } else {
            $ran = "full"
            & $exe -NoProfile -ExecutionPolicy Bypass -File $Runner
        }
        $battery = $LASTEXITCODE
        if ($battery -eq 3) { Stop-Land 3 "the battery was DEFERRED; nothing landed, rerun Land.ps1 later" }
        if ($battery -ne 0) {
            $suspects = if ($ran -eq "full" -and $owed.Count -gt 0) { " Landed on -Quick since the last complete run, so suspects too: $($owed -join '; ')." } else { "" }
            Stop-Land 1 "the battery failed (exit $battery); nothing landed.$suspects A waiver landing is merged by hand (PROJECT_CONTEXT.md)"
        }
        if ($ran -eq "quick") {
            $body += "RunTests.ps1 -Quick PASS on the tree at $head, run by Land.ps1 with $waiters landing(s) queued behind; the complete battery is owed to the last of them."
        } else {
            $body += "Complete RunTests.ps1 PASS on the tree at $head, run by Land.ps1."
            if ($owed.Count -gt 0) { $body += "It covers the landings made on -Quick before it: $($owed -join '; ')." }
            $stamp = Get-TreeStamp -Root $wt -Kind "green" -Key $headKey
            if ($stamp) { $body += @("Green stamp from $($stamp.time) at $($stamp.commit):") + @($stamp.stages | ForEach-Object { "  $_" }) }
        }
    }

    # --- Record, recheck main, merge ---------------------------------------------------------------
    $msgFile = Join-Path $common "csvm-landing-msg.txt"
    [IO.File]::WriteAllText($msgFile, ($body -join "`n") + "`n", (New-Object System.Text.UTF8Encoding($false)))
    $null = Invoke-Git $wt commit -q --allow-empty -F $msgFile
    Remove-Item -LiteralPath $msgFile -Force -ErrorAction SilentlyContinue

    $mainNow = (Invoke-Git $wt rev-parse main) -join ""
    if ($mainNow -ne $mainAt) { Stop-Land 5 "main moved from $($mainAt.Substring(0, 9)) to $($mainNow.Substring(0, 9)) outside the queue during the battery; nothing landed, rerun Land.ps1" }
    $out = Invoke-Git $mainRoot merge --no-ff $branch -m $Message
    if ($GitExit -ne 0) {
        $out | ForEach-Object { Write-Host "    $_" }
        Stop-Land 2 "the merge into main failed; look at $mainRoot before anything else"
    }
    $landed = (Invoke-Git $mainRoot rev-parse --short HEAD) -join ""
    if ($ran -eq "quick") { [IO.File]::AppendAllText($owedFile, "$landed $branch`n") }
    if ($ran -eq "full") { Remove-Item -LiteralPath $owedFile -Force -ErrorAction SilentlyContinue }
    Stop-Land 0 "landed $branch on main as $landed"
} finally {
    Remove-Item -LiteralPath $ticket -Force -ErrorAction SilentlyContinue
    if ($held) {
        Remove-Item -LiteralPath $holderFile -Force -ErrorAction SilentlyContinue
        $mutex.ReleaseMutex()
    }
    $mutex.Dispose()
}
