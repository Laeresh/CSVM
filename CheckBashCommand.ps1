#!/usr/bin/env pwsh
# The Bash guard every harness's pre-tool hook calls on Windows: the Bash tool runs git and gh,
# and the PowerShell tool runs everything else. git and gh behave the same in either shell, and
# Bash is the one where a gh --jq filter keeps its embedded double quotes (Windows PowerShell 5.1
# strips them from a native command's argument).
#
# A command passes when every statement in it starts with git or gh, or is a read-only filter
# (head, tail, grep, wc, sort, uniq) that a pipe feeds. Quoted text is read as one word, so a "|"
# inside a --jq filter or a commit message does not split it.
#
# Pure ASCII on purpose (PROJECT_CONTEXT.md). Exit code 2 blocks, which is what a PreToolUse hook
# needs.
#
# Usage:
#   <hook payload on stdin> | ./CheckBashCommand.ps1
#   ./CheckBashCommand.ps1 -Command 'git log | head -5'
#   ./CheckBashCommand.ps1 -SelfTest
[CmdletBinding()]
param(
    [string]$Command,
    [switch]$SelfTest
)

$q = [char]39

# $true when $CommandLine may run in the Bash tool.
function Test-BashCommand {
    param([string]$CommandLine)
    # Quoted text first, so its separators are not read as the shell's. Then the fd duplications
    # (2>&1), whose "&" is not the background operator.
    $bare = $CommandLine -replace ($q + '[^' + $q + ']*' + $q), '_' -replace '"(?:[^"\\]|\\.)*"', '_'
    $bare = $bare -replace '\d*>&\d+', ''
    $parts = [regex]::Split($bare, '(&&|\|\||[;|&\r\n])')
    $after = ''
    for ($i = 0; $i -lt $parts.Count; $i += 2) {
        $segment = $parts[$i].Trim()
        if ($segment) {
            $ok = $segment -match '^(git|gh)\b' -or
                ($after -eq '|' -and $segment -match '^(head|tail|grep|wc|sort|uniq)\b')
            if (-not $ok) { return $false }
        }
        if ($i + 1 -lt $parts.Count) { $after = $parts[$i + 1] }
    }
    return $true
}

if ($SelfTest) {
    $failed = 0
    $cases = @(
        @('git status', $true),
        @('git log --oneline | head -5', $true),
        @('git -C Z:/wt diff && git status', $true),
        @('git log 2>&1 | tail -3', $true),
        @('gh issue list | grep -i bot | wc -l', $true),
        @(('gh issue list --json n --jq ' + $q + '.[] | "\(.n)"' + $q), $true),
        @('gh pr view 5 --jq ".title | ascii_downcase"', $true),
        @(('git commit -m ' + $q + 'a; b | c' + $q), $true),
        @('ls', $false),
        @('git diff && cat file.cs', $false),
        @('head -5 file.txt', $false),
        @('git log; head -5 file.txt', $false),
        @(('git status' + [char]10 + 'rm -rf x'), $false),
        @('git status & rm -rf x', $false),
        @('git log | xargs rm', $false)
    )
    foreach ($c in $cases) {
        $got = Test-BashCommand -CommandLine $c[0]
        $mark = if ($got -eq $c[1]) { 'PASS' } else { $failed++; 'FAIL' }
        Write-Host ('  {0}  {1,-5}  {2}' -f $mark, $c[1], ($c[0] -replace '\r?\n', ' \n '))
    }
    Write-Host ''
    Write-Host ('{0} passed, {1} failed' -f ($cases.Count - $failed), $failed)
    if ($failed -gt 0) { exit 1 }
    exit 0
}

if ($IsMacOS -or $IsLinux) { exit 0 }
if (-not $Command -and [Console]::IsInputRedirected) {
    $raw = [Console]::In.ReadToEnd()
    if ($raw) {
        try { $Command = [string]($raw | ConvertFrom-Json).tool_input.command } catch { $Command = '' }
    }
}
if (-not $Command) { exit 0 }
if (Test-BashCommand -CommandLine $Command) { exit 0 }
[Console]::Error.WriteLine('Use the PowerShell tool. The Bash tool runs only git and gh commands, optionally piped')
[Console]::Error.WriteLine('into head, tail, grep, wc, sort or uniq; everything else goes through PowerShell.')
exit 2
