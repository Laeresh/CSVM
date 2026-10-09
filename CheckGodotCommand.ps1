#!/usr/bin/env pwsh
# The Godot guard every harness's pre-tool hook calls, for the Bash and the PowerShell tool alike:
# an agent shell command must not invoke the Godot binary (Godot_v4*.exe) directly. A direct launch
# bypasses the memory ledger's admission (MemoryLedger.ps1), the hidden desktop and the run's job
# object; RunProbe.ps1 is the same launch with all three. The engine still registers a launch that
# slips past this guard, so the others' admissions count it, but nothing can make it wait.
#
# Only an invocation is blocked: the binary's path at the start of a statement (unquoted, or after
# the call operator), or as what Start-Process, Process::Start or cmd /c runs. A mention (Test-Path,
# Get-Process, a grep for the name) passes.
#
# Pure ASCII on purpose (PROJECT_CONTEXT.md). Exit code 2 blocks, which is what a PreToolUse hook
# needs.
#
# Usage:
#   <hook payload on stdin> | ./CheckGodotCommand.ps1
#   ./CheckGodotCommand.ps1 -Command '& tools\godot\Godot_v4.7-stable_mono_win64.exe --path CSVM'
#   ./CheckGodotCommand.ps1 -SelfTest
[CmdletBinding()]
param(
    [string]$Command,
    [switch]$SelfTest
)

# $true when $CommandLine invokes the Godot binary directly.
function Test-GodotInvocation {
    param([string]$CommandLine)
    foreach ($m in [regex]::Matches($CommandLine, 'Godot_v4[^\s"'']*\.exe')) {
        # The statement up to the binary's name, less the path in front of the name.
        $before = $CommandLine.Substring(0, $m.Index)
        $before = [regex]::Split($before, '&&|\|\||[;|\r\n{]')[-1]
        if ($before -match 'Process\]::Start\(\s*(["''][^"'']*|[^\s"'']*)$') { return $true }
        $before = [regex]::Split($before, '\(')[-1]
        # A quoted path alone at a statement start is a string, not a call, in PowerShell.
        $quoted = $before -match '["''][^"'']*$'
        $before = $before -replace '(["''][^"'']*|[^\s"'']*)$', ''
        $before = $before.Trim()
        # An editor import (CheckUidSidecars.ps1's .uid fix) builds no session and is let through.
        $after = [regex]::Split($CommandLine.Substring($m.Index + $m.Length), '&&|\|\||[;|\r\n]')[0]
        if ($after -match '(^|\s)--import\b') { continue }
        if (($before -eq '' -and -not $quoted) -or $before -match '^(&|\.)$' -or
            $before -match '(^|\s)(Start-Process|saps|start)(\s+-FilePath)?$' -or
            $before -match '(^|\s)-FilePath$' -or
            $before -match '(^|\s)cmd(\.exe)?\s+/[ck]$') {
            return $true
        }
    }
    return $false
}

if ($SelfTest) {
    $failed = 0
    $exe = 'Z:\CSVM\tools\godot\Godot_v4.7-stable_mono_win64\Godot_v4.7-stable_mono_win64.exe'
    $cases = @(
        @("& '$exe' --path CSVM res://scenes/Main.tscn -- --det --frames=420", $true),
        @("& `"Z:\Some Path\Godot_v4.7-stable_mono_win64.exe`" --path CSVM", $true),
        @("$exe --path CSVM --headless res://scenes/Main.tscn -- --run-tests", $true),
        @("cd Z:\CSVM; tools\godot\Godot_v4.7-stable_mono_win64\Godot_v4.7-stable_mono_win64.exe --path CSVM", $true),
        @("Start-Process -FilePath '$exe' -ArgumentList '--path','CSVM'", $true),
        @("Start-Process $exe -ArgumentList x", $true),
        @("[System.Diagnostics.Process]::Start('$exe', '--path CSVM')", $true),
        @("cmd /c $exe --path CSVM", $true),
        @("git status && $exe --det", $true),
        @("Test-Path '$exe'", $false),
        @("Get-Process Godot_v4.7-stable_mono_win64 -ErrorAction SilentlyContinue", $false),
        @("Get-CimInstance Win32_Process -Filter `"Name LIKE 'Godot_v4%.exe'`"", $false),
        @('rg "Godot_v4.*\.exe" docs', $false),
        @(('$g = "' + $exe + '"'), $false),
        @("& '$($exe -replace 'win64\.exe$', 'win64_console.exe')' --path ""Z:\CSVM\CSVM"" --headless --import", $false),
        @(("@('" + $exe + "')"), $false),
        @(("`$paths = @('" + $exe + "', 'x')"), $false),
        @('.\RunProbe.ps1 --det --frames=420 --screenshot=Z:\CSVM\.scratch\a.png', $false)
    )
    foreach ($c in $cases) {
        $got = Test-GodotInvocation -CommandLine $c[0]
        $mark = if ($got -eq $c[1]) { 'PASS' } else { $failed++; 'FAIL' }
        Write-Host ('  {0}  {1,-5}  {2}' -f $mark, $c[1], $c[0])
    }
    Write-Host ''
    Write-Host ('{0} passed, {1} failed' -f ($cases.Count - $failed), $failed)
    if ($failed -gt 0) { exit 1 }
    exit 0
}

if (-not $Command -and [Console]::IsInputRedirected) {
    $raw = [Console]::In.ReadToEnd()
    if ($raw) {
        try { $Command = [string]($raw | ConvertFrom-Json).tool_input.command } catch { $Command = '' }
    }
}
if (-not $Command) { exit 0 }
if (-not (Test-GodotInvocation -CommandLine $Command)) { exit 0 }
[Console]::Error.WriteLine('BLOCKED: this command runs the Godot binary directly. Launch it through .\RunProbe.ps1 <user args>')
[Console]::Error.WriteLine('(or .\RunTests.ps1), which admits it against the machine-wide memory ledger, runs it on the')
[Console]::Error.WriteLine('hidden desktop and joins it to the run job. See docs/tooling.md, "The memory ledger".')
exit 2
