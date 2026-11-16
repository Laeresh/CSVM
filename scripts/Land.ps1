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
        Set-Content -LiteralPath (Join-Path $main "a.txt") -Value "base"
        $null = Invoke-Git $main add -A
        $null = Invoke-Git $main commit -q -m base

        $wt = New-Branch "pass" "b.txt" "pass"
        $code = Invoke-Land $wt "pass"
        $merged = (Invoke-Git $main log -1 --format=%s main) -join ""
        Assert-Land ($code -eq 0 -and $merged -eq "Merge pass" -and (Test-Path (Join-Path $main "b.txt"))) "a green branch lands with a merge commit ($code, '$merged')"
        $verify = (Invoke-Git $main log -1 --format=%s main^2) -join ""
        Assert-Land ($verify -like "Verification: pass with main merged in*") "the branch carries a Verification commit ('$verify')"

        $before = (Invoke-Git $main rev-parse main) -join ""
        $wt = New-Branch "red" "c.txt" "red"
        $code = Invoke-Land $wt "fail"
        Assert-Land ($code -eq 1 -and ((Invoke-Git $main rev-parse main) -join "") -eq $before) "a red battery exits 1 and leaves main alone ($code)"

        $wt = New-Branch "clash" "a.txt" "branch side"
        Set-Content -LiteralPath (Join-Path $main "a.txt") -Value "main side"
        $null = Invoke-Git $main commit -q -am "main edits a"
        $code = Invoke-Land $wt "pass"
        $gitDir = (Invoke-Git $wt rev-parse --absolute-git-dir) -join ""
        $mergeHead = Test-Path -LiteralPath (Join-Path $gitDir "MERGE_HEAD")
        Assert-Land ($code -eq 4 -and -not $mergeHead) "a conflict exits 4 with the merge aborted ($code)"

        $wt = New-Branch "moved" "d.txt" "moved"
        $code = Invoke-Land $wt "move"
        $tip = (Invoke-Git $main log -1 --format=%s main) -join ""
        Assert-Land ($code -eq 5 -and $tip -eq "direct") "main moved under the run exits 5 without merging ($code, '$tip')"

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

# --- The queue -------------------------------------------------------------------------------------
$mutex = New-Object System.Threading.Mutex($false, $LockName)
$held = $false
try {
    while (-not $held) {
        try { $held = $mutex.WaitOne(60000) } catch [System.Threading.AbandonedMutexException] { $held = $true }
        if (-not $held) {
            $holder = "unknown"
            try { $holder = [IO.File]::ReadAllText($holderFile) } catch { }
            Write-Host "  Land: waiting for the landing in progress: $holder"
        }
    }
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

    # --- The battery, as a child so its exit cannot end this script --------------------------------
    $exe = (Get-Process -Id $PID).Path
    & $exe -NoProfile -ExecutionPolicy Bypass -File $Runner
    $battery = $LASTEXITCODE
    if ($battery -eq 3) { Stop-Land 3 "the battery was DEFERRED; nothing landed, rerun Land.ps1 later" }
    if ($battery -ne 0) { Stop-Land 1 "the battery failed (exit $battery); nothing landed. A waiver landing is merged by hand (PROJECT_CONTEXT.md)" }

    # --- Record, recheck main, merge ---------------------------------------------------------------
    $head = (Invoke-Git $wt rev-parse --short HEAD) -join ""
    $stamp = Get-TreeStamp -Root $wt -Kind "green" -Key (Get-TreeKey -Root $wt -Paths $GreenKeyPaths -Commit "HEAD")
    $body = @("Verification: $branch with main merged in ($($mainAt.Substring(0, 9)))", "",
        "Complete RunTests.ps1 PASS on the tree at $head, run by Land.ps1.")
    if ($stamp) { $body += @("Green stamp from $($stamp.time) at $($stamp.commit):") + @($stamp.stages | ForEach-Object { "  $_" }) }
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
    Stop-Land 0 "landed $branch on main as $landed"
} finally {
    if ($held) {
        Remove-Item -LiteralPath $holderFile -Force -ErrorAction SilentlyContinue
        $mutex.ReleaseMutex()
    }
    $mutex.Dispose()
}
