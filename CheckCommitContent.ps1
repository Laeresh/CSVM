#!/usr/bin/env pwsh
# The content gate every harness's pre-commit hook calls: encoding, item IDs, golden prose,
# comment caps, doc entries.
#
# THE ROOT IS THE WHOLE POINT. A hook runs as its own process in whatever directory the session
# happens to be in, which is not necessarily the tree the commit will write to. Taking the hook's
# own directory and stopping there checked one tree and cleared a commit into another, and a
# corrupted character reached main that way. Two shapes move the target tree away from the process
# directory, and each is read off the command rather than guessed: the command naming another tree
# (git -C, --work-tree, --git-dir), and a directory change inside the same command
# (Set-Location <path>; git commit), which the hook cannot observe because it runs first but which
# is written in the command string it is handed.
#
# So the target is the tree that command will write to: the named tree, else the directory it
# changes to first, else the tree the hook stands in. ONE tree, never a sweep of all of them.
#
# DO NOT WIDEN THIS BACK TO EVERY WORKTREE. A sweep blocks a commit on a file in a tree the
# session cannot fix, which with a dozen live worktrees means another session's UNCOMMITTED work
# stops yours, and the only way past is the escape hatch, so the gate ends up teaching people to
# skip it. Content arriving by merge or pull is still caught: it lands in the tree that merged it,
# which is the tree that tree's next commit checks.
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
#   ./CheckCommitContent.ps1 -ShowRoots -Command '...'   which tree that command would check
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
    @{ Name = 'comment caps';    Script = 'CheckCommentCaps.ps1' },
    @{ Name = 'doc entries';     Script = 'CheckDocEntries.ps1' }
)

# THE GATE IS ONLY AS GOOD AS ITS TRIGGER. What follows is what a git commit INVOCATION looks like
# on a command line, and it is deliberately not "git" followed by "commit": the very form CLAUDE.md
# prescribes for naming the tree a commit writes to, "git -C <tree> commit", puts a global option
# between the two words, so a trigger testing for that adjacency waves every worktree-scoped commit
# through with no check run at all. Four comment-cap violations and an over-cap doc entry reached
# main that way.
#
# Only git's OWN globals are allowed in the gap, and the match must begin at a statement boundary.
# Both narrowings are the point. Allowing arbitrary tokens there would make
# "git log --grep='a git commit'" a commit; matching the bare word would make "git log --grep=commit"
# one; and starting anywhere would make a commit quoted inside another command's argument one.
$q = [char]39
$anyToken = '(?:"[^"]*"|' + $q + '[^' + $q + ']*' + $q + '|[^\s]+)'
$gitGlobal = '(?:-[Cc]\s+' + $anyToken +
    '|--(?:git-dir|work-tree|namespace|exec-path|super-prefix|config-env)(?:=|\s+)' + $anyToken +
    '|--[a-z][a-z-]*|-[a-zA-Z])'
$commitInvocation = '(?:^|[\r\n;|&{(])\s*&?\s*git(?:\s+' + $gitGlobal + ')*\s+commit(?:\s|$)'

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

# Which directory does this command move to before it commits? Empty when it does not move. Only
# a change standing BEFORE the commit counts, since one after it cannot affect where it lands, and
# the last such change wins because that is the directory the commit runs in.
function Get-ChangedDirectory {
    param([string]$CommandLine)
    $commit = [regex]::Match($CommandLine, $commitInvocation)
    $upTo = if ($commit.Success) { $CommandLine.Substring(0, $commit.Index) } else { $CommandLine }
    $q = [char]39
    $pathGroup = '("[^"]*"|' + $q + '[^' + $q + ']*' + $q + '|[^\s;|&]+)'
    # Anchored on a statement boundary so a bare "cd" inside a word or a flag value never matches,
    # and the optional -Path/-LiteralPath is how Set-Location is spelled when it is spelled out.
    $pattern = '(?:^|[;|&]|^\s*)\s*(?:Set-Location|chdir|sl|cd)\s+(?:-(?:Literal)?Path\s+)?' + $pathGroup
    $found = ''
    foreach ($m in [regex]::Matches($upTo, $pattern)) {
        $candidate = Get-UnquotedPath -Raw $m.Groups[1].Value
        if ($candidate -and -not $candidate.StartsWith('-')) { $found = $candidate }
    }
    return $found
}

function Get-TargetRoots {
    param([string]$CommandLine, [string]$From)
    if (-not $From) { $From = $scriptRoot }
    # In precedence order, each read off the command itself. A candidate that does not resolve to
    # a tree is not a reason to wave the commit through, so the next one is tried and the hook's
    # own tree is the floor.
    $named = Get-NamedTree -CommandLine $CommandLine
    if ($named) {
        $top = Resolve-Toplevel -Path $named
        if ($top) { return @($top) }
    }
    $moved = Get-ChangedDirectory -CommandLine $CommandLine
    if ($moved) {
        if (-not [IO.Path]::IsPathRooted($moved)) { $moved = Join-Path $From $moved }
        $top = Resolve-Toplevel -Path $moved
        if ($top) { return @($top) }
    }
    $own = Resolve-Toplevel -Path $From
    if ($own) { return @($own) }
    return @()
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
    [Console]::Error.WriteLine('These checks run against the tree this commit writes to, named above:')
    [Console]::Error.WriteLine('the one the command names, else the one it changes to, else the one the')
    [Console]::Error.WriteLine('hook stands in. The file named above is in that tree, so it is yours to fix.')
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

        # Row 3: a directory change inside the same command is read off the command string, since
        # the hook runs before it and would otherwise check the tree being left.
        $r3 = @(Get-TargetRoots -CommandLine ('Set-Location ' + $wt + '; git commit -m x') -From $main)
        Assert-Row 'row 3  same-call Set-Location names the tree moved to' (
            $r3.Count -eq 1 -and $r3[0] -ieq $wtNormal)
        Assert-Row 'row 3  and the fault the hook cannot see blocks' (
            @(Invoke-Checks -Roots $r3).Count -ge 1)
        $r3b = @(Get-TargetRoots -CommandLine ('cd "' + $wt + '"; git commit -m x') -From $main)
        Assert-Row 'row 3  a quoted cd is the same move' (
            $r3b.Count -eq 1 -and $r3b[0] -ieq $wtNormal)
        $r3c = @(Get-TargetRoots -CommandLine ('git commit -m x; Set-Location ' + $wt) -From $main)
        Assert-Row 'row 3  a move AFTER the commit is not the commit tree' (
            $r3c.Count -eq 1 -and $r3c[0] -ieq (ConvertTo-NormalPath -Path $main))

        # Row 4: no tree named and no move means the tree the hook stands in, and ONLY that one.
        # A fault in a sibling worktree is not this commit's to fix and must not block it.
        $r4 = @(Get-TargetRoots -CommandLine 'git commit -m x' -From $main)
        Assert-Row 'row 4  a bare commit checks the hook own tree alone' (
            $r4.Count -eq 1 -and $r4[0] -ieq (ConvertTo-NormalPath -Path $main))
        Assert-Row 'row 4  and a sibling worktree fault does not block it' (
            @(Invoke-Checks -Roots $r4).Count -eq 0)

        $quoted = Get-NamedTree -CommandLine ('git -C "' + $wt + '" commit -m x')
        Assert-Row 'row 2  a quoted -C path is unquoted' ($quoted -ieq $wt)

        # Row 7: content that arrived by merge rather than by an edit is simply present in the
        # tree that merged it, so that tree's own next commit is where it blocks.
        Assert-Row 'row 7  merged-in content blocks at the next commit in its own tree' (
            @(Invoke-Checks -Roots $r2).Count -ge 1)

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

        # Row 11: CheckDocEntries.ps1 as the fifth check. A minimal architecture split (one
        # namespace file, one index bullet, one matching CSVM/src file) so the existence and
        # coverage checks pass and only the cap violation under test fires.
        $fxDoc = Join-Path $base 'fxdoc'
        Write-Chars -File (Join-Path $fxDoc 'docs/architecture.md') -Codes ([int[]][char[]](
            ('# Architecture' + [char]10 + [char]10 +
             '## Module index' + [char]10 + [char]10 +
             '### src/Test/' + [char]10 + [char]10 +
             '- `src/Test/Thing.cs` - a thing that does things.' + [char]10)))
        Write-Chars -File (Join-Path $fxDoc 'CSVM/src/Test/Thing.cs') -Codes (
            [int[]][char[]]('// fixture only' + [char]10))
        $overCapBody = (1..13 | ForEach-Object { 'line' + $_ }) -join ([char]10)
        Write-Chars -File (Join-Path $fxDoc 'docs/architecture/Test.md') -Codes ([int[]][char[]](
            ('## src/Test/Thing.cs' + [char]10 + [char]10 + $overCapBody + [char]10)))
        $r11 = @(Invoke-Checks -Roots @($fxDoc))
        $names11 = @($r11 | ForEach-Object { $_.Check })
        Assert-Row 'row 11 an over-cap architecture entry blocks, naming the file' (
            ($names11 -contains 'doc entries') -and
            ($r11 | Where-Object { $_.Check -eq 'doc entries' } |
                ForEach-Object { $_.Output -join [char]10 } |
                Select-String -Pattern 'docs/architecture/Test\.md.*cap 8').Count -gt 0)

        $withinCapBody = (1..8 | ForEach-Object { 'line' + $_ }) -join ([char]10)
        Write-Chars -File (Join-Path $fxDoc 'docs/architecture/Test.md') -Codes ([int[]][char[]](
            ('## src/Test/Thing.cs' + [char]10 + [char]10 + $withinCapBody + [char]10)))
        Assert-Row 'row 11 a within-cap architecture split passes' (
            (@(Invoke-Checks -Roots @($fxDoc)) | Where-Object { $_.Check -eq 'doc entries' }).Count -eq 0)

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
        & (Join-Path $scriptRoot 'CheckCommitContent.ps1') -Command (
            'git --work-tree=' + $wt + ' commit -m x') | Out-Null
        Assert-Row 'guard  a --work-tree-scoped commit is checked end to end' ($LASTEXITCODE -eq 2)

        # The trigger itself. It is the single point of failure for the whole gate: every harness
        # calls this script unconditionally and this pattern decides whether anything runs, so the
        # forms git accepts between "git" and its subcommand are enumerated here rather than
        # trusted on inspection.
        function Assert-Trigger {
            param([string]$Name, [string]$CommandLine, [bool]$Expected)
            Assert-Row $Name (($CommandLine -match $commitInvocation) -eq $Expected)
        }
        Assert-Trigger 'trigger  a bare commit' 'git commit -m x' $true
        Assert-Trigger 'trigger  git -C <tree> commit' 'git -C Z:\wt commit -m x' $true
        Assert-Trigger 'trigger  git -C with a quoted path holding a space' (
            'git -C "Z:\a b\wt" commit -m x') $true
        Assert-Trigger 'trigger  git --work-tree= and --git-dir= commit' (
            'git --work-tree=Z:\wt --git-dir=Z:\wt\.git commit -m x') $true
        Assert-Trigger 'trigger  a spaced --work-tree value' 'git --work-tree Z:\wt commit -m x' $true
        Assert-Trigger 'trigger  git -c k=v commit' 'git -c user.email=x commit -m x' $true
        Assert-Trigger 'trigger  a valueless global before the subcommand' (
            'git --no-pager -C Z:\wt commit -F msg.txt') $true
        Assert-Trigger 'trigger  a commit after another statement' 'git add -A; git commit -m x' $true
        Assert-Trigger 'trigger  a commit after &&' 'git add -A && git commit -m x' $true
        Assert-Trigger 'trigger  a commit after a Set-Location' 'Set-Location Z:\wt; git commit -m x' $true
        Assert-Trigger 'trigger  a commit through the call operator' '& git commit -m x' $true
        Assert-Trigger 'trigger  a commit inside a braced block' 'if ($ok) { git commit -m x }' $true
        Assert-Trigger 'trigger  git log --grep=commit is not a commit' 'git log --grep=commit' $false
        Assert-Trigger 'trigger  a commit quoted inside another git command is not one' (
            'git log --grep="a git commit here"') $false
        Assert-Trigger 'trigger  a commit quoted inside a message is not an invocation' (
            'Write-Host "git commit -m x"') $false
        Assert-Trigger 'trigger  a path merely naming the runner is not a commit' (
            'Get-Content Z:\CSVM\CheckCommitContent.ps1') $false
        Assert-Trigger 'trigger  commit-tree is a different subcommand' (
            'git commit-tree HEAD -m x') $false
        Assert-Trigger 'trigger  a non-global token before commit is not a commit' (
            'git log commit') $false
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
    # The one trigger every harness relies on; see $commitInvocation above for why it is shaped
    # the way it is. No harness carries a trigger of its own, because three that did all carried
    # the same wrong one.
    if ($Command -notmatch $commitInvocation) { exit 0 }
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
