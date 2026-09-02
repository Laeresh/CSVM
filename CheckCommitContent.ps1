#!/usr/bin/env pwsh
# The content gate every harness's pre-commit hook calls: encoding, item IDs, golden prose,
# comment caps.
#
# THE ROOT IS THE WHOLE POINT. A hook runs as its own process in whatever directory the session
# happens to be in, which is not necessarily the tree the commit will write to. Deriving the root
# from the hook's own directory therefore checked one tree and cleared a commit into another, and
# a corrupted character reached main that way. Two shapes move the target tree away from the
# process directory: the command naming another tree (git -C, --work-tree, --git-dir), and a
# directory change inside the same command (Set-Location <path>; git commit), which the hook
# cannot see because it runs before the command does.
#
# So: when the command names a tree, check that tree. Otherwise check EVERY worktree, which costs
# a second and needs no guess about which one the commit will land in. Sweeping is also what
# catches content that arrived by merge or pull, since a pre-tool hook on a merge would inspect
# the tree before the content got there.
#
# The payload's cwd is deliberately not consulted: the harness sets this process's directory to
# exactly that value, so it is the same answer as git rev-parse here and adds nothing. That was
# measured, not assumed.
#
# Pure ASCII on purpose (PROJECT_CONTEXT.md). Every check READS; nothing here writes to a repo.
# Exit code 2 on a violation, which is what a PreToolUse hook needs in order to block.
#
# Usage:
#   <hook payload on stdin> | ./CheckCommitContent.ps1
#   ./CheckCommitContent.ps1 -Command 'git -C ../wt commit -m x'
#   ./CheckCommitContent.ps1 -Root <path>      check one tree, no derivation
#   ./CheckCommitContent.ps1 -ShowRoots -Command '...'   which trees that command would check
#   ./CheckCommitContent.ps1 -SelfTest         exercise the whole gate against fixtures
[CmdletBinding()]
param(
    [string]$Command,
    [string[]]$Root,
    [switch]$ShowRoots,
    [switch]$SelfTest
)

$scriptRoot = $PSScriptRoot
if (-not $scriptRoot) { $scriptRoot = (Get-Location).Path }

$checks = @(
    @{ Name = 'encoding';        Script = 'CheckEncoding.ps1' },
    @{ Name = 'item IDs';        Script = 'CheckItemIds.ps1' },
    @{ Name = 'golden prose';    Script = 'CheckGoldenProse.ps1' },
    @{ Name = 'comment caps';    Script = 'CheckCommentCaps.ps1' }
)

# A path as written on the command line, minus the quoting.
function Get-UnquotedPath {
    param([string]$Raw)
    $t = $Raw.Trim()
    if ($t.Length -ge 2) {
        $q = $t[0]
        if (($q -eq '"' -or $q -eq [char]39) -and $t[$t.Length - 1] -eq $q) {
            return $t.Substring(1, $t.Length - 2)
        }
    }
    return $t
}

# Which tree will this command write to? Empty when it does not say, which is the sweep case.
function Get-NamedTree {
    param([string]$CommandLine)
    # Each element is parenthesised because "," binds tighter than "+" in PowerShell: without the
    # parentheses these three concatenations collapse into one nested array and nothing matches.
    $q = [char]39
    $pathGroup = '("[^"]*"|' + $q + '[^' + $q + ']*' + $q + '|[^\s]+)'
    $patterns = @(
        ('(?:^|\s)-C\s+' + $pathGroup),
        ('--work-tree[=\s]+' + $pathGroup),
        ('--git-dir[=\s]+' + $pathGroup)
    )
    foreach ($p in $patterns) {
        $m = [regex]::Match($CommandLine, $p)
        if ($m.Success) { return Get-UnquotedPath -Raw $m.Groups[1].Value }
    }
    return ''
}

# git speaks forward slashes and PowerShell speaks backslashes; a trailing separator makes two
# spellings of the same tree compare unequal, which is how a swept root gets checked twice.
function ConvertTo-NormalPath {
    param([string]$Path)
    if (-not $Path) { return '' }
    return ($Path -replace '/', '\').TrimEnd('\')
}

function Resolve-Toplevel {
    param([string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) { return '' }
    $top = git -C $Path rev-parse --show-toplevel 2>$null
    if (-not $top) { return '' }
    return (ConvertTo-NormalPath -Path ([string]$top))
}

function Get-AllWorktrees {
    param([string]$From)
    $out = @()
    foreach ($line in @(git -C $From worktree list --porcelain 2>$null)) {
        if ($line -match '^worktree\s+(.+)$') { $out += (ConvertTo-NormalPath -Path $Matches[1]) }
    }
    if ($out.Count -eq 0) {
        $top = Resolve-Toplevel -Path $From
        if ($top) { $out = @($top) }
    }
    return $out
}

function Get-TargetRoots {
    param([string]$CommandLine, [string]$From)
    if (-not $From) { $From = $scriptRoot }
    $named = Get-NamedTree -CommandLine $CommandLine
    if ($named) {
        $top = Resolve-Toplevel -Path $named
        # A named tree that does not resolve is not a reason to wave the commit through: fall
        # through to the sweep rather than returning nothing to check.
        if ($top) { return @($top) }
    }
    return @(Get-AllWorktrees -From $From)
}

# Runs every check against every root. Returns the failure report, empty when all clean.
function Invoke-Checks {
    param([string[]]$Roots)
    $failures = @()
    foreach ($r in $Roots) {
        if (-not (Test-Path -LiteralPath $r)) { continue }
        foreach ($c in $checks) {
            $script = Join-Path $scriptRoot $c.Script
            if (-not (Test-Path -LiteralPath $script -PathType Leaf)) { continue }
            $out = & $script -Root $r 2>&1
            if ($LASTEXITCODE -eq 0) { continue }
            $failures += [pscustomobject]@{
                Root  = $r
                Check = $c.Name
                Output = ($out | ForEach-Object { [string]$_ })
            }
        }
    }
    return $failures
}

function Write-Failures {
    param([object[]]$Failures)
    foreach ($f in $Failures) {
        [Console]::Error.WriteLine('')
        [Console]::Error.WriteLine('BLOCKED by the ' + $f.Check + ' check in ' + $f.Root)
        foreach ($line in $f.Output) { [Console]::Error.WriteLine('  ' + $line) }
    }
    [Console]::Error.WriteLine('')
    [Console]::Error.WriteLine('These checks run against every worktree, not just the one you are in,')
    [Console]::Error.WriteLine('because the tree a commit writes to is not always the directory the hook')
    [Console]::Error.WriteLine('sits in. Fix the file named above - it is in the worktree named above.')
    [Console]::Error.WriteLine('To land something first, set CSVM_SKIP_CONTENT_CHECKS=1 for that command.')
}

# ---------------------------------------------------------------------------------------------
# Self-test. Builds throwaway fixtures and a real linked worktree, never a junction or symlink:
# a reparse point left behind turns a later recursive delete into a delete of its target.
# ---------------------------------------------------------------------------------------------
function Invoke-SelfTest {
    $base = Join-Path ([IO.Path]::GetTempPath()) ('csvm-contentgate-' + [Guid]::NewGuid().ToString('N').Substring(0, 8))
    $main = Join-Path $base 'main'
    $wt = Join-Path $base 'wt'
    $utf8 = New-Object System.Text.UTF8Encoding($false)
    $script:passed = 0
    $script:failed = 0

    function Write-Chars {
        param([string]$File, [int[]]$Codes)
        $dir = Split-Path -Parent $File
        if (-not (Test-Path -LiteralPath $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
        $s = ($Codes | ForEach-Object { [char]$_ }) -join ''
        [IO.File]::WriteAllText($File, $s, $utf8)
    }

    function Assert-Row {
        param([string]$Name, [bool]$Condition)
        if ($Condition) { Write-Host ('  PASS  ' + $Name); $script:passed++ }
        else { Write-Host ('  FAIL  ' + $Name); $script:failed++ }
    }

    try {
        New-Item -ItemType Directory -Path $main -Force | Out-Null
        git -C $main init -q 2>&1 | Out-Null
        git -C $main config user.email 'selftest@example.invalid' | Out-Null
        git -C $main config user.name 'selftest' | Out-Null
        Write-Chars -File (Join-Path $main 'README.md') -Codes @(0x68, 0x69)
        git -C $main add -A 2>&1 | Out-Null
        git -C $main commit -q -m 'seed' 2>&1 | Out-Null
        git -C $main worktree add -q -b selftest-branch $wt 2>&1 | Out-Null

        $cleanRoots = @($main, $wt)
        Assert-Row 'row 1  clean trees pass' (@(Invoke-Checks -Roots $cleanRoots).Count -eq 0)

        # Rows 5 and 6: the validator separates a valid pair from a lost character.
        # 0.1 x-sign en-dash 4 x-sign, then a lost em dash.
        Write-Chars -File (Join-Path $main 'ok.md') -Codes @(0x30, 0x2E, 0x31, 0x00D7, 0x2013, 0x34)
        git -C $main add -A 2>&1 | Out-Null
        Assert-Row 'row 5  a valid character pair passes' (@(Invoke-Checks -Roots @($main)).Count -eq 0)

        Write-Chars -File (Join-Path $main 'lost.md') -Codes @(0x00E2, 0x20AC, 0x201D)
        git -C $main add -A 2>&1 | Out-Null
        $r6 = @(Invoke-Checks -Roots @($main))
        Assert-Row 'row 6  a lost em dash blocks' ($r6.Count -eq 1 -and $r6[0].Check -eq 'encoding')
        Remove-Item -LiteralPath (Join-Path $main 'lost.md') -Force
        git -C $main add -A 2>&1 | Out-Null

        # Row 8: untracked but not ignored is still about to be committed.
        Write-Chars -File (Join-Path $main 'untracked.md') -Codes @(0x00E2, 0x0161, 0x00A0)
        Assert-Row 'row 8  untracked-not-ignored blocks' (@(Invoke-Checks -Roots @($main)).Count -ge 1)
        Remove-Item -LiteralPath (Join-Path $main 'untracked.md') -Force

        # Rows 2, 3, 4 and 7 all turn on which roots get derived. Plant the fault in the worktree
        # and commit it there, which is also the merge case: the content is simply present.
        Write-Chars -File (Join-Path $wt 'bad.md') -Codes @(0x00E2, 0x0161, 0x00A0)
        git -C $wt add -A 2>&1 | Out-Null
        git -C $wt commit -q -m 'plant' 2>&1 | Out-Null

        $wtNormal = ConvertTo-NormalPath -Path $wt
        $r2 = @(Get-TargetRoots -CommandLine ('git -C ' + $wt + ' commit -m x'))
        Assert-Row 'row 2  -C names the worktree and nothing else' (
            $r2.Count -eq 1 -and $r2[0] -ieq $wtNormal)
        Assert-Row 'row 2  and the fault there blocks' (@(Invoke-Checks -Roots $r2).Count -ge 1)

        # The sweep is what covers a directory change the hook cannot see, so it has to reach the
        # worktree while the command names only the main checkout.
        $r3 = @(Get-TargetRoots -CommandLine ('Set-Location ' + $wt + '; git commit -m x') -From $main)
        Assert-Row 'row 3  same-call Set-Location sweeps every worktree' ($r3.Count -eq 2)
        Assert-Row 'row 3  and the fault the hook cannot see blocks' (
            @(Invoke-Checks -Roots $r3).Count -ge 1)

        $r4 = @(Get-TargetRoots -CommandLine 'git commit -m x' -From $main)
        Assert-Row 'row 4  a bare commit sweeps every worktree' ($r4.Count -eq 2)
        Assert-Row 'row 4  and an unrelated tree fault blocks' (
            @(Invoke-Checks -Roots $r4).Count -ge 1)

        $quoted = Get-NamedTree -CommandLine ('git -C "' + $wt + '" commit -m x')
        Assert-Row 'row 2  a quoted -C path is unquoted' ($quoted -ieq $wt)

        Assert-Row 'row 7  merged-in content blocks at the next commit' (
            @(Invoke-Checks -Roots @($wt)).Count -ge 1)

        # Row 10: the other three checks still fire after extraction.
        $fx = Join-Path $base 'fx'
        New-Item -ItemType Directory -Path $fx -Force | Out-Null
        git -C $fx init -q 2>&1 | Out-Null
        Write-Chars -File (Join-Path $fx 'backlog.md') -Codes (
            [int[]][char[]]("- ``BL-001`` one" + [char]10 + "- ``BL-001`` two" + [char]10))
        Write-Chars -File (Join-Path $fx 'analysis/goldens/manifest.json') -Codes (
            [int[]][char[]]('{"shots":[{"name":"s","exercises":"pinned 2026-01-02"}]}'))
        Write-Chars -File (Join-Path $fx 'CSVM/src/Bad.cs') -Codes ([int[]][char[]](
            ("// a" + [char]10 + "// b" + [char]10 + "// c" + [char]10 + "// d" + [char]10 +
             "// e" + [char]10 + "var x = 1;" + [char]10)))
        $r10 = @(Invoke-Checks -Roots @($fx))
        $names = @($r10 | ForEach-Object { $_.Check })
        Assert-Row 'row 10 duplicate item ID blocks' ($names -contains 'item IDs')
        Assert-Row 'row 10 golden prose blocks' ($names -contains 'golden prose')
        Assert-Row 'row 10 comment cap blocks' ($names -contains 'comment caps')

        # Row 9: the escape hatch.
        $env:CSVM_SKIP_CONTENT_CHECKS = '1'
        $hatch = & (Join-Path $scriptRoot 'CheckCommitContent.ps1') -Command 'git commit -m x'
        $hatchCode = $LASTEXITCODE
        Remove-Item Env:\CSVM_SKIP_CONTENT_CHECKS
        Assert-Row 'row 9  the escape hatch passes' ($hatchCode -eq 0)

        # A command that is not a commit must never pay for any of this.
        $skip = & (Join-Path $scriptRoot 'CheckCommitContent.ps1') -Command 'git status'
        Assert-Row 'guard  a non-commit command exits early' ($LASTEXITCODE -eq 0)

        # Every row above tests a helper. These two drive the script's own entry point, which is
        # where the guard lives and where a -C-scoped commit used to exit 0 before any check ran.
        & (Join-Path $scriptRoot 'CheckCommitContent.ps1') -Command ('git -C ' + $wt + ' commit -m x') | Out-Null
        Assert-Row 'guard  a -C-scoped commit is checked end to end' ($LASTEXITCODE -eq 2)
        & (Join-Path $scriptRoot 'CheckCommitContent.ps1') -Command 'git log --grep=commit' | Out-Null
        Assert-Row 'guard  a subcommand merely naming commit is not one' ($LASTEXITCODE -eq 0)
    }
    finally {
        if (Test-Path -LiteralPath $wt) { git -C $main worktree remove --force $wt 2>&1 | Out-Null }
        git -C $main worktree prune 2>&1 | Out-Null
        if (Test-Path -LiteralPath $base) { Remove-Item -LiteralPath $base -Recurse -Force -ErrorAction SilentlyContinue }
    }

    Write-Host ''
    Write-Host ('{0} passed, {1} failed' -f $passed, $failed)
    if ($failed -gt 0) { exit 1 }
    exit 0
}

if ($SelfTest) { Invoke-SelfTest }

if (-not $Command -and [Console]::IsInputRedirected) {
    $raw = [Console]::In.ReadToEnd()
    if ($raw) {
        try { $Command = [string]($raw | ConvertFrom-Json).tool_input.command } catch { $Command = '' }
    }
}

if ($ShowRoots) {
    if (-not $Root) { $Root = Get-TargetRoots -CommandLine $Command }
    foreach ($r in $Root) { Write-Output $r }
    exit 0
}

if (-not $Root) {
    if (-not $Command) { exit 0 }
    # A commit names its tree BEFORE the subcommand, as "git -C <tree> commit", so testing for a
    # bare "git commit" waves every worktree-scoped commit through with no check run at all.
    if ($Command -notmatch '(?:^|[\s;|&])git(?:\s+\S+)*?\s+commit(?:\s|$)') { exit 0 }
    if ($env:CSVM_SKIP_CONTENT_CHECKS) {
        [Console]::Error.WriteLine('CSVM_SKIP_CONTENT_CHECKS is set: content checks skipped.')
        exit 0
    }
    $Root = Get-TargetRoots -CommandLine $Command
}

$failures = @(Invoke-Checks -Roots $Root)
if ($failures.Count -eq 0) { exit 0 }
Write-Failures -Failures $failures
exit 2
