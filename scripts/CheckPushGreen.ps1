#!/usr/bin/env pwsh
# The pre-push check: a push to main goes through only when the pushed commit carries a green
# stamp, which a complete RunTests.ps1 PASS writes for the content it judged (TreeStampCore.ps1).
# The stamp is keyed on CSVM/, CSVM.Tests/ and analysis/goldens/, so a commit that changes none of
# them keeps the stamp of the commit before it. git reaches this through scripts/hooks/pre-push.
#
# Pure ASCII on purpose (PROJECT_CONTEXT.md).
#
# Usage:
#   <git's pre-push lines on stdin> | ./scripts/CheckPushGreen.ps1    the hook; exit 1 blocks the push
#   ./scripts/CheckPushGreen.ps1 -Commit <rev>     say whether that commit carries a stamp
#   ./scripts/CheckPushGreen.ps1 -Install          copy scripts/hooks/pre-push into the git directory
#   ./scripts/CheckPushGreen.ps1 -SelfTest         keys, stamps and the hook against a throwaway repo
[CmdletBinding()]
param(
    [string]$Commit,
    [switch]$Install,
    [switch]$SelfTest
)

. (Join-Path $PSScriptRoot 'TreeStampCore.ps1')
$GuardedRef = 'refs/heads/main'

# The stamp on a commit, or $null.
function Get-GreenStamp {
    param([string]$Root, [string]$Rev)
    return (Get-TreeStamp -Root $Root -Kind 'green' -Key (Get-TreeKey -Root $Root -Paths $GreenKeyPaths -Commit $Rev))
}

# The hook's verdict over git's pre-push lines ("<local ref> <local sha> <remote ref> <remote sha>"):
# the commits pushed to main without a stamp. A deleted ref (an all-zero local sha) pushes nothing.
function Get-UnstampedPushes {
    param([string]$Root, [string[]]$Lines)
    $missing = @()
    foreach ($line in $Lines) {
        $f = @($line.Trim() -split '\s+')
        if ($f.Count -lt 4 -or $f[2] -ne $GuardedRef -or $f[1] -match '^0+$') { continue }
        if (-not (Get-GreenStamp -Root $Root -Rev $f[1])) { $missing += $f[1] }
    }
    return $missing
}

function Invoke-SelfTest {
    $base = Join-Path ([IO.Path]::GetTempPath()) ('csvm-pushgreen-' + [Guid]::NewGuid().ToString('N').Substring(0, 8))
    $script:passed = 0
    $script:failed = 0
    function Assert-Row {
        param([string]$Name, [bool]$Condition)
        if ($Condition) { Write-Host ('  PASS  ' + $Name); $script:passed++ }
        else { Write-Host ('  FAIL  ' + $Name); $script:failed++ }
    }
    function Write-Text {
        param([string]$Rel, [string]$Text)
        $path = Join-Path $base $Rel
        $null = New-Item -ItemType Directory -Path (Split-Path -Parent $path) -Force
        [IO.File]::WriteAllText($path, $Text, (New-Object System.Text.UTF8Encoding($false)))
    }
    function Invoke-Git { git -C $base -c user.name=t -c user.email=t@invalid -c core.autocrlf=false @args 2>&1 | Out-Null }
    $zero = '0' * 40
    try {
        $null = New-Item -ItemType Directory -Path $base -Force
        Invoke-Git init -q
        Write-Text 'CSVM/a.cs' 'class A { }'
        Write-Text 'CSVM.Tests/t.cs' 'class T { }'
        Write-Text 'analysis/goldens/manifest.json' '{}'
        Write-Text 'docs/x.md' 'x'
        Write-Text '.gitignore' 'CSVM/bin/'
        Invoke-Git add -A
        Invoke-Git commit -q -m one
        $first = [string](git -C $base rev-parse HEAD)

        $k1 = Get-TreeKey -Root $base -Paths $GreenKeyPaths
        Assert-Row 'key      a clean working tree has a key' ($k1.Length -eq 40)
        Assert-Row 'key      the working tree and its commit share it' ($k1 -eq (Get-TreeKey -Root $base -Paths $GreenKeyPaths -Commit $first))
        Assert-Row 'key      a path the tree lacks does not empty it' ((Get-TreeKey -Root $base -Paths ($GreenKeyPaths + 'no/such')) -eq $k1)
        Write-Text 'docs/x.md' 'y'
        Assert-Row 'key      a change outside the paths keeps it' ((Get-TreeKey -Root $base -Paths $GreenKeyPaths) -eq $k1)
        Write-Text 'CSVM/bin/x.dll' 'ignored'
        Assert-Row 'key      an ignored file keeps it' ((Get-TreeKey -Root $base -Paths $GreenKeyPaths) -eq $k1)
        Write-Text 'CSVM/b.cs' 'class B { }'
        $k2 = Get-TreeKey -Root $base -Paths $GreenKeyPaths
        Assert-Row 'key      an untracked file under the paths moves it' ($k2.Length -eq 40 -and $k2 -ne $k1)
        $status = @(git -C $base status --porcelain)
        Assert-Row 'key      reading it stages nothing' (($status -contains '?? CSVM/b.cs') -and ($status -contains ' M docs/x.md'))
        Assert-Row 'key      outside a repository it is empty' (-not (Get-TreeKey -Root ([IO.Path]::GetTempPath()) -Paths @('no-such-path-here')))

        Assert-Row 'stamp    absent before it is written' ($null -eq (Get-TreeStamp -Root $base -Kind 'green' -Key $k2))
        Set-TreeStamp -Root $base -Kind 'green' -Key $k2 -Fields @{ time = 'now'; tree = 'here' }
        Assert-Row 'stamp    read back with its fields' ((Get-TreeStamp -Root $base -Kind 'green' -Key $k2).tree -eq 'here')
        Assert-Row 'stamp    one kind does not answer for another' ($null -eq (Get-TreeStamp -Root $base -Kind 'format' -Key $k2))
        Invoke-Git add -A
        Invoke-Git commit -q -m two
        $second = [string](git -C $base rev-parse HEAD)
        Assert-Row 'stamp    the commit of a stamped working tree is stamped' ($null -ne (Get-GreenStamp -Root $base -Rev $second))
        Write-Text 'docs/x.md' 'z'
        Invoke-Git commit -q -a -m docs
        $docs = [string](git -C $base rev-parse HEAD)
        Assert-Row 'stamp    a docs-only commit keeps it' ($null -ne (Get-GreenStamp -Root $base -Rev $docs))

        Assert-Row 'hook     a stamped commit pushes to main' (@(Get-UnstampedPushes -Root $base -Lines @("refs/heads/main $docs refs/heads/main $zero")).Count -eq 0)
        Assert-Row 'hook     an unstamped commit does not' (@(Get-UnstampedPushes -Root $base -Lines @("refs/heads/main $first refs/heads/main $zero"))[0] -eq $first)
        Assert-Row 'hook     another branch is not checked' (@(Get-UnstampedPushes -Root $base -Lines @("refs/heads/wip $first refs/heads/wip $zero")).Count -eq 0)
        Assert-Row 'hook     deleting main pushes nothing' (@(Get-UnstampedPushes -Root $base -Lines @("(delete) $zero refs/heads/main $first")).Count -eq 0)

        # The entry point, as git runs it: the lines on stdin, the pushed repository as the directory.
        $self = Join-Path $PSScriptRoot 'CheckPushGreen.ps1'
        $shell = (Get-Process -Id $PID).Path
        Push-Location $base
        try {
            "refs/heads/main $first refs/heads/main $zero" | & $shell -NoProfile -ExecutionPolicy Bypass -File $self 2>&1 | Out-Null
            Assert-Row 'entry    an unstamped push to main exits 1' ($LASTEXITCODE -eq 1)
            "refs/heads/main $docs refs/heads/main $zero" | & $shell -NoProfile -ExecutionPolicy Bypass -File $self 2>&1 | Out-Null
            Assert-Row 'entry    a stamped push to main exits 0' ($LASTEXITCODE -eq 0)
        } finally { Pop-Location }
    } finally {
        if (Test-Path -LiteralPath $base) { Remove-Item -LiteralPath $base -Recurse -Force -ErrorAction SilentlyContinue }
    }
    Write-Host ''
    Write-Host ('{0} passed, {1} failed' -f $passed, $failed)
    if ($failed -gt 0) { exit 1 }
    exit 0
}

if ($SelfTest) { Invoke-SelfTest }

$root = [string](git rev-parse --show-toplevel 2>$null)
if (-not $root) { exit 0 }

if ($Install) {
    $common = [string](git -C $root rev-parse --git-common-dir)
    if (-not [IO.Path]::IsPathRooted($common)) { $common = Join-Path $root $common }
    $target = Join-Path (Join-Path $common 'hooks') 'pre-push'
    $source = Join-Path $PSScriptRoot 'hooks/pre-push'
    if ((Test-Path -LiteralPath $target) -and ((Get-FileHash -LiteralPath $target).Hash -ne (Get-FileHash -LiteralPath $source).Hash)) {
        Write-Host "A different pre-push hook is already at $target; merge $source into it by hand."
        exit 1
    }
    Copy-Item -LiteralPath $source -Destination $target -Force
    Write-Host "installed $target"
    exit 0
}

if ($Commit) {
    $stamp = Get-GreenStamp -Root $root -Rev $Commit
    if ($stamp) { Write-Host "green: $Commit passed the complete battery at $($stamp.time) in $($stamp.tree)"; exit 0 }
    Write-Host "no green stamp: $Commit has not passed the complete battery on this machine"
    exit 1
}

if (-not [Console]::IsInputRedirected) { exit 0 }
$lines = @([Console]::In.ReadToEnd() -split "`n" | Where-Object { $_.Trim() })
$missing = @(Get-UnstampedPushes -Root $root -Lines $lines)
if ($missing.Count -eq 0) { exit 0 }
foreach ($sha in $missing) {
    [Console]::Error.WriteLine("BLOCKED: $($sha.Substring(0, 9)) is pushed to main with no green stamp: its CSVM/, CSVM.Tests/ and")
    [Console]::Error.WriteLine('analysis/goldens/ have not passed the complete battery on this machine.')
}
[Console]::Error.WriteLine('Run the complete .\scripts\RunTests.ps1 in a worktree at that commit, then push again.')
[Console]::Error.WriteLine('"git push --no-verify" skips this check, for a landing over a waived red.')
exit 1
