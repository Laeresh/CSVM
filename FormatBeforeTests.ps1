#!/usr/bin/env pwsh
# The format-before-tests gate every harness's pre-tool hook calls: dotnet format, then a build
# that blocks on any StyleCop warning dotnet format could not fix.
#
# IT FIRES ON AN INVOCATION, NEVER A MENTION. The old guard was a substring test over the whole
# command, so reading, grepping or listing RunTests.ps1 rebuilt the assembly as surely as running
# it did, and a golden battery reading that tree mid-rebuild found no assembly at all
# (docs/verification.md INSTR-42). A statement counts only when it BEGINS with the runner (bare,
# through "&", or as "powershell -File"), "dotnet test" or "git commit". Where a statement begins
# is GateCommand.ps1's decision, shared with the content gate so the two cannot drift apart.
#
# THE BUILD IS A REBUILD ON PURPOSE. Analyzer warnings are emitted only when the compiler runs,
# and an incremental build of an up-to-date project skips CoreCompile, so a plain build would
# report a clean tree whenever nothing changed since the last build. That is exactly the commit
# case, and it would make the check silent where it matters.
#
# THE TREE IS THE ONE THE COMMAND NAMES. dotnet format WRITES, so formatting the wrong tree is
# worse than checking it. The runner's own path names a tree (an absolute RunTests.ps1 lives in
# one), then a "git -C <tree>" or --work-tree, then a directory change ahead of the invocation
# (Set-Location <tree>; .\RunTests.ps1), and only when none does is the process directory used.
# The hook runs before the command, so that move is read off the command string, as the content
# gate reads it; ignoring it formatted the session's own tree (often the main checkout) while the
# runner tested another. A tree written as a variable is read from a quoted literal assigned to it
# earlier in the same command, and an invocation naming its tree any other way through a variable
# or expression is BLOCKED.
#
# Pure ASCII on purpose (PROJECT_CONTEXT.md). Exit code 2 blocks the tool call.
#
# Usage:
#   <hook payload on stdin> | ./FormatBeforeTests.ps1
#   ./FormatBeforeTests.ps1 -Command '.\RunTests.ps1 -Quick'
#   ./FormatBeforeTests.ps1 -ShowRoot -Command '...'   which tree that command would build, if any
#   ./FormatBeforeTests.ps1 -SelfTest                  exercise the trigger and tree resolution
[CmdletBinding()]
param(
    [string]$Command,
    [switch]$ShowRoot,
    [switch]$SelfTest
)

$scriptRoot = $PSScriptRoot
if (-not $scriptRoot) { $scriptRoot = (Get-Location).Path }

# The command-line reading both gates share. A missing helper is reported, not turned into a block:
# a gate that cannot parse would otherwise refuse every shell command the session runs.
$gateCommand = Join-Path $scriptRoot 'GateCommand.ps1'
if (-not (Test-Path -LiteralPath $gateCommand -PathType Leaf)) {
    [Console]::Error.WriteLine('FormatBeforeTests.ps1: ' + $gateCommand + ' is missing, so the format gate did not run.')
    exit 1
}
. $gateCommand
$q = $GateQuote

# What this gate guards, each at the head of a statement: the runner, written bare, through "&" or
# behind "powershell -File" / "pwsh -File" with any host options before -File; "dotnet test"; and a
# git commit. One pattern, so the first invocation in the command is the one that is resolved.
$runnerPath = '(?<path>"[^"]*RunTests\.ps1"|' + $q + '[^' + $q + ']*RunTests\.ps1' + $q + '|[^\s"' + $q + ']*RunTests\.ps1)'
$hostFile = '(?:(?:[^\s"' + $q + ']*[\\/])?(?i:powershell|pwsh)(?i:\.exe)?(?:\s+(?!(?i:-file)\s)\S+)*?\s+(?i:-file)\s+)?'
$formatInvocation = $GateStatementStart + '(?:(?<runner>' + $hostFile + $runnerPath + '(?:\s|$))' +
    '|(?<test>dotnet\s+test(?:\s|$))|(?<commit>' + $GateGitCommit + '))'

# What, if anything, this command invokes. Returns Kind ('runner', 'test', 'commit'), the tree the
# invocation itself names (the runner's directory, or the commit's -C/--work-tree), and Before, the
# command ahead of it, which is where a move or an assignment the invocation depends on must be.
function Get-Invocation {
    param([string]$CommandLine)
    $m = [regex]::Match($CommandLine, $formatInvocation)
    if (-not $m.Success) { return $null }
    $before = $CommandLine.Substring(0, $m.Index)
    if ($m.Groups['runner'].Success) {
        $path = Get-UnquotedPath -Raw $m.Groups['path'].Value
        $dir = Split-Path -Parent $path
        if ($dir -eq '.' -or $dir -eq '.\' -or $dir -eq './') { $dir = '' }
        return [pscustomobject]@{ Kind = 'runner'; Tree = $dir; Before = $before }
    }
    if ($m.Groups['test'].Success) {
        return [pscustomobject]@{ Kind = 'test'; Tree = ''; Before = $before }
    }
    return [pscustomobject]@{ Kind = 'commit'; Tree = (Get-NamedTree -CommandLine $m.Value); Before = $before }
}

# The tree to format and build as Root, empty when the command invokes nothing, or Unresolved when
# the command names its tree through a variable or expression; the caller blocks that case. The
# precedence is GateCommand.ps1's Resolve-CommandTree, led by the invocation's own tree.
function Resolve-TargetTree {
    param([string]$CommandLine, [string]$From)
    if (-not $From) { $From = (Get-Location).Path }
    $inv = Get-Invocation -CommandLine $CommandLine
    if (-not $inv) { return [pscustomobject]@{ Root = ''; Unresolved = '' } }
    return Resolve-CommandTree -CommandLine $CommandLine -Before $inv.Before -From $From -First @($inv.Tree)
}

function Get-TargetRoot {
    param([string]$CommandLine, [string]$From)
    return (Resolve-TargetTree -CommandLine $CommandLine -From $From).Root
}

# Formats, rebuilds, and returns the StyleCop lines dotnet format left behind (SA0001 excluded).
function Invoke-FormatAndBuild {
    param([string]$Root)
    $proj = Join-Path $Root 'CSVM/CSVM.csproj'
    if (-not (Test-Path -LiteralPath $proj)) { return @() }
    dotnet format $proj --verbosity quiet
    $buildOutput = dotnet build $proj --verbosity quiet --nologo -t:Rebuild 2>&1
    return @($buildOutput | Select-String -Pattern 'SA\d{4}' | Where-Object { $_.Line -notmatch 'SA0001' } | ForEach-Object { $_.Line })
}

# ---------------------------------------------------------------------------------------------
# Self-test. Exercises the trigger and the tree resolution against two throwaway repos. It never
# formats or builds anything.
# ---------------------------------------------------------------------------------------------
function Invoke-SelfTest {
    $base = Join-Path ([IO.Path]::GetTempPath()) ('csvm-formatgate-' + [Guid]::NewGuid().ToString('N').Substring(0, 8))
    $main = Join-Path $base 'main'
    $other = Join-Path $base 'other'
    $script:passed = 0
    $script:failed = 0

    function Assert-Row {
        param([string]$Name, [bool]$Condition)
        if ($Condition) { Write-Host ('  PASS  ' + $Name); $script:passed++ }
        else { Write-Host ('  FAIL  ' + $Name); $script:failed++ }
    }

    function Assert-Fires {
        param([string]$Name, [string]$CommandLine, [string]$Kind)
        $inv = Get-Invocation -CommandLine $CommandLine
        if ($Kind) { Assert-Row $Name ($null -ne $inv -and $inv.Kind -eq $Kind) }
        else { Assert-Row $Name ($null -eq $inv) }
    }

    try {
        foreach ($d in @($main, $other)) {
            New-Item -ItemType Directory -Path $d -Force | Out-Null
            git -C $d init -q 2>&1 | Out-Null
            [IO.File]::WriteAllText((Join-Path $d 'RunTests.ps1'), 'exit 0', (New-Object System.Text.UTF8Encoding($false)))
        }
        # macOS's temp folder sits behind the /var -> /private/var link and git reports the
        # physical path, so the expected trees are taken from git's own spelling.
        if ($IsMacOS -or $IsLinux) {
            $main = Resolve-Toplevel -Path $main
            $other = Resolve-Toplevel -Path $other
        }

        # Mentions never fire.
        Assert-Fires 'mention  Get-Content .\RunTests.ps1' 'Get-Content .\RunTests.ps1' ''
        Assert-Fires 'mention  grep RunTests.ps1 CLAUDE.md' 'grep RunTests.ps1 CLAUDE.md' ''
        Assert-Fires 'mention  Get-Content RunTests.ps1 | Select-Object -First 5' 'Get-Content RunTests.ps1 | Select-Object -First 5' ''
        Assert-Fires 'mention  echo x | Select-String RunTests.ps1' 'echo x | Select-String RunTests.ps1' ''
        Assert-Fires 'mention  git log --grep=commit' 'git log --grep=commit' ''
        Assert-Fires 'mention  git log --grep=RunTests.ps1' 'git log --grep=RunTests.ps1' ''
        Assert-Fires 'mention  dotnet build then a dotnet test flag mid-segment' 'dotnet build CSVM --no-restore; echo "dotnet test"' ''
        Assert-Fires 'mention  a quoted runner mid-segment' 'Write-Host "run .\RunTests.ps1 later"' ''
        Assert-Fires 'mention  Test-Path RunTests.ps1' 'Test-Path RunTests.ps1' ''

        # Invocations fire, with their kind.
        Assert-Fires 'runner   .\RunTests.ps1 -Quick' '.\RunTests.ps1 -Quick' 'runner'
        Assert-Fires 'runner   & ".\RunTests.ps1" -Quick' '& ".\RunTests.ps1" -Quick' 'runner'
        Assert-Fires 'runner   after a leading git -C rev-parse' ('git -C ' + $other + ' rev-parse --show-toplevel; .\RunTests.ps1 -Quick') 'runner'
        Assert-Fires 'runner   after an unrelated first segment' 'Get-Date; .\RunTests.ps1 -Suite c1' 'runner'
        Assert-Fires 'runner   an absolute path' ($other + '\RunTests.ps1 -Quick') 'runner'
        Assert-Fires 'runner   with -SkipUnits' '.\RunTests.ps1 -Suite c1 -SkipUnits -SkipGoldens' 'runner'
        Assert-Fires 'test     dotnet test CSVM.Tests' 'dotnet test CSVM.Tests' 'test'
        Assert-Fires 'commit   git commit -m x' 'git commit -m x' 'commit'
        Assert-Fires 'commit   git -C wt commit -F msg.txt' ('git -C ' + $other + ' commit -F msg.txt') 'commit'
        Assert-Fires 'commit   git add -A && git commit -m x' 'git add -A && git commit -m x' 'commit'
        Assert-Fires 'commit   a message mentioning the runner still commits' 'git commit -m "quotes RunTests.ps1"' 'commit'
        Assert-Fires 'commit   git -c k=v commit' 'git -c user.email=x commit -m x' 'commit'
        Assert-Fires 'commit   git --work-tree= --git-dir= commit' 'git --work-tree=Z:\wt --git-dir=Z:\wt\.git commit -m x' 'commit'
        Assert-Fires 'commit   a valueless global before the subcommand' 'git --no-pager -C Z:\wt commit -F msg.txt' 'commit'
        Assert-Fires 'commit   a quoted -C path holding a space' 'git -C "Z:\a b\wt" commit -m x' 'commit'
        Assert-Fires 'mention  git log --grep="a git commit here"' 'git log --grep="a git commit here"' ''
        Assert-Fires 'mention  git commit-tree is a different subcommand' 'git commit-tree HEAD -m x' ''
        Assert-Fires 'mention  a non-global token before commit' 'git log commit' ''

        # Statement boundaries beyond ";", "&&", "||" and "|", and the runner behind a host. These
        # are the forms the format gate's own splitter once missed while the content gate fired.
        $nl = [string][char]10
        Assert-Fires 'commit   after a newline' ('git add -A' + $nl + 'git commit -m x') 'commit'
        Assert-Fires 'commit   after a CRLF' ('git add -A' + [char]13 + $nl + 'git commit -m x') 'commit'
        Assert-Fires 'commit   inside a braced block' 'if ($ok) { git commit -m x }' 'commit'
        Assert-Fires 'commit   through the call operator' '& git commit -m x' 'commit'
        Assert-Fires 'runner   after a newline' ('Get-Date' + $nl + '.\RunTests.ps1') 'runner'
        Assert-Fires 'runner   inside a braced block' 'if ($ok) { .\RunTests.ps1 -Quick }' 'runner'
        Assert-Fires 'runner   powershell -File' 'powershell -File .\RunTests.ps1' 'runner'
        Assert-Fires 'runner   powershell.exe with options before -File' (
            'powershell.exe -NoProfile -ExecutionPolicy Bypass -File "' + $other + '\RunTests.ps1" -Quick') 'runner'
        Assert-Fires 'runner   pwsh -file in lower case' 'pwsh -nop -file ./RunTests.ps1' 'runner'
        Assert-Fires 'test     dotnet test after a newline' ('dotnet build' + $nl + 'dotnet test CSVM.Tests') 'test'
        Assert-Fires 'mention  powershell -File running another script' 'powershell -File .\Other.ps1 RunTests.ps1' ''
        Assert-Fires 'mention  powershell -Command reading the runner' 'powershell -Command Get-Content .\RunTests.ps1' ''
        Assert-Fires 'mention  a runner path in a here-string body' ('$s = @"' + $nl + 'see .\RunTests.ps1 -Quick' + $nl + '"@') ''

        # Tree resolution: the invocation names it, a -C or --work-tree names it, a move ahead of
        # the invocation names it, the process directory is last.
        $r1 = Get-TargetRoot -CommandLine ($other + '\RunTests.ps1 -Quick') -From $main
        Assert-Row 'tree     an absolute runner path names its own tree' ($r1 -ieq $other)
        $r2 = Get-TargetRoot -CommandLine ('git -C ' + $other + ' commit -m x') -From $main
        Assert-Row 'tree     git -C names the commit tree' ($r2 -ieq $other)
        $r3 = Get-TargetRoot -CommandLine ('git -C "' + $other + '" rev-parse --show-toplevel; .\RunTests.ps1 -Quick') -From $main
        Assert-Row 'tree     a leading quoted git -C reaches a relative runner' ($r3 -ieq $other)
        $r4 = Get-TargetRoot -CommandLine '.\RunTests.ps1 -Quick' -From $main
        Assert-Row 'tree     a bare runner uses the process directory' ($r4 -ieq $main)
        # The runner tests the tree it is moved to, so that is the tree to format; the session's
        # own tree (often the main checkout) is one the command never touches.
        $r5 = Get-TargetRoot -CommandLine ('Set-Location ' + $other + '; .\RunTests.ps1 -Quick') -From $main
        Assert-Row 'tree     a same-call Set-Location names the tree moved to' ($r5 -ieq $other)
        $r5b = Get-TargetRoot -CommandLine ('Set-Location "' + $other + '"' + $nl + '.\RunTests.ps1 -Quick') -From $main
        Assert-Row 'tree     a quoted Set-Location on its own line is the same move' ($r5b -ieq $other)
        $r5c = Get-TargetRoot -CommandLine ('.\RunTests.ps1 -Quick; Set-Location ' + $other) -From $main
        Assert-Row 'tree     a move AFTER the runner is not its tree' ($r5c -ieq $main)
        $r5d = Get-TargetRoot -CommandLine ('Push-Location ' + $main + '; & ' + $other + '\RunTests.ps1') -From $main
        Assert-Row 'tree     an absolute runner path outranks a move' ($r5d -ieq $other)
        $r5e = Get-TargetRoot -CommandLine ('Set-Location ' + $base + '; ' + (Join-Path 'other' 'RunTests.ps1')) -From $main
        Assert-Row 'tree     a relative runner path is taken from the directory moved to' ($r5e -ieq $other)
        $r5f = Resolve-TargetTree -CommandLine 'Set-Location $wt; .\RunTests.ps1' -From $main
        Assert-Row 'tree     a move to an unassigned variable is unresolved' ($r5f.Unresolved -eq '$wt' -and -not $r5f.Root)
        $r11 = Get-TargetRoot -CommandLine ('git --work-tree=' + $other + ' --git-dir=' + (Join-Path $other '.git') + ' commit -m x') -From $main
        Assert-Row 'tree     --work-tree names the commit tree' ($r11 -ieq $other)
        $r12 = Get-TargetRoot -CommandLine ('git --git-dir "' + (Join-Path $other '.git') + '" commit -m x') -From $main
        Assert-Row 'tree     a lone --git-dir names the tree around it' ($r12 -ieq $other)
        $r13 = Get-TargetRoot -CommandLine ('powershell -NoProfile -File ' + (Join-Path $other 'RunTests.ps1') + ' -Quick') -From $main
        Assert-Row 'tree     a powershell -File runner names its own tree' ($r13 -ieq $other)
        $r14 = Get-TargetRoot -CommandLine ('git add -A' + $nl + 'git -C ' + $other + ' commit -m x') -From $main
        Assert-Row 'tree     a commit after a newline keeps its -C tree' ($r14 -ieq $other)
        $r6 = Get-TargetRoot -CommandLine 'Get-Content .\RunTests.ps1' -From $main
        Assert-Row 'tree     a mention resolves no tree' (-not $r6)
        # dotnet format WRITES, so a lowercase -c read as -C would format a tree named by a config
        # key rather than by a path.
        $r7 = Get-TargetRoot -CommandLine 'git -c user.email=x commit -m x' -From $main
        Assert-Row 'tree     -c is a config key and names no tree' ($r7 -ieq $main)

        # A tree written as a variable: a quoted literal assigned in the same command is read, and
        # anything else is unresolved, which blocks rather than formatting the process directory.
        $r8 = Get-TargetRoot -CommandLine ('$wt = ' + $q + $other + $q + '; git -C $wt commit -m x') -From $main
        Assert-Row 'tree     a -C variable assigned a literal resolves' ($r8 -ieq $other)
        $r9 = Get-TargetRoot -CommandLine ('$wt="' + $other + '"; & "$wt\RunTests.ps1" -Quick') -From $main
        Assert-Row 'tree     a runner path through an assigned variable resolves' ($r9 -ieq $other)
        $r10 = Get-TargetRoot -CommandLine ('$wt = ' + $q + $other + $q + '; git -C $wt rev-parse; .\RunTests.ps1') -From $main
        Assert-Row 'tree     a leading git -C variable reaches a relative runner' ($r10 -ieq $other)
        foreach ($case in @(
                @('an unassigned -C variable', 'git -C $wt commit -m x', '$wt'),
                @('a -C expression', 'git -C (Get-Tree) commit -m x', '(Get-Tree)'),
                @('a runner under an unassigned variable', '& "$wt\RunTests.ps1" -Quick', '$wt'),
                @('a variable assigned a non-literal', '$wt = Join-Path a b; git -C $wt commit -m x', '$wt'))) {
            $t = Resolve-TargetTree -CommandLine $case[1] -From $main
            Assert-Row ('tree     ' + $case[0] + ' is unresolved') ($t.Unresolved -eq $case[2] -and -not $t.Root)
        }
        $t = Resolve-TargetTree -CommandLine 'git -C $wt status' -From $main
        Assert-Row 'tree     a variable on a non-invocation is never unresolved' (-not $t.Unresolved -and -not $t.Root)

        # The entry point, end to end. A mention exits 0 having printed nothing under -ShowRoot.
        $shown = @(& (Join-Path $scriptRoot 'FormatBeforeTests.ps1') -ShowRoot -Command 'Get-Content .\RunTests.ps1')
        Assert-Row 'entry    -ShowRoot on a mention prints nothing' ($LASTEXITCODE -eq 0 -and $shown.Count -eq 0)
        $shown = @(& (Join-Path $scriptRoot 'FormatBeforeTests.ps1') -ShowRoot -Command ($other + '\RunTests.ps1'))
        Assert-Row 'entry    -ShowRoot on an invocation prints its tree' ($shown.Count -eq 1 -and $shown[0] -ieq $other)
        # The refusal exits before anything is formatted, so the entry point is safe to drive here.
        & (Join-Path $scriptRoot 'FormatBeforeTests.ps1') -Command 'git -C $nowhere commit -m x' | Out-Null
        Assert-Row 'entry    an unresolved -C variable blocks a commit' ($LASTEXITCODE -eq 2)
        & (Join-Path $scriptRoot 'FormatBeforeTests.ps1') -Command 'git -C $nowhere status' | Out-Null
        Assert-Row 'entry    an unresolved variable on a non-invocation passes' ($LASTEXITCODE -eq 0)
    }
    finally {
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
if (-not $Command) { exit 0 }

$target = Resolve-TargetTree -CommandLine $Command
$root = $target.Root
if ($ShowRoot) {
    if ($target.Unresolved) {
        [Console]::Error.WriteLine('unresolved: ' + $target.Unresolved + ' (this invocation is blocked)')
    }
    if ($root) { Write-Output $root }
    exit 0
}
# Only a command Get-Invocation has already called an invocation reaches this refusal.
if ($target.Unresolved) {
    [Console]::Error.WriteLine('BLOCKED: this command names its tree through ' + $target.Unresolved + ', which the')
    [Console]::Error.WriteLine('format gate cannot resolve, and dotnet format writes to the tree it picks. Write')
    [Console]::Error.WriteLine('the path literally, or assign it a quoted literal in the same command first.')
    exit 2
}
if (-not $root) { exit 0 }

$remaining = @(Invoke-FormatAndBuild -Root $root)
if ($remaining.Count -eq 0) { exit 0 }
foreach ($line in $remaining) { [Console]::Error.WriteLine($line) }
[Console]::Error.WriteLine('StyleCop warnings dotnet format could not auto-fix remain above (SA0001 excluded) - fix them by hand, then retry.')
exit 2
