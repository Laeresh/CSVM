#!/usr/bin/env pwsh
# Codex PreToolUse hook — mirrors .claude/settings.json.
# Before the agent runs RunTests.ps1 / dotnet test / git commit, run `dotnet format`,
# rebuild, and block (exit 2) if StyleCop warnings dotnet format could not auto-fix remain.

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$proj = Join-Path $repo 'CSVM/CSVM.csproj'

$raw = [Console]::In.ReadToEnd()
if (-not $raw) { exit 0 }

try { $j = $raw | ConvertFrom-Json } catch { exit 0 }

# Codex names its exec tool differently per shell_tool mode (shell / shell_command /
# exec_command / unified_exec), and the command lands in a different field in each.
# Matching the serialized tool_input covers every variant.
$commandText = if ($null -ne $j.tool_input) { $j.tool_input | ConvertTo-Json -Depth 10 -Compress } else { '' }
if ($commandText -notmatch 'RunTests\.ps1|dotnet\s+test|git\s+commit') { exit 0 }

dotnet format $proj --verbosity quiet
$buildOutput = dotnet build $proj --verbosity quiet --nologo -t:Rebuild 2>&1

$remaining = $buildOutput | Select-String -Pattern 'SA\d{4}' | Where-Object { $_.Line -notmatch 'SA0001' }
if ($remaining) {
    $remaining | ForEach-Object { [Console]::Error.WriteLine($_.Line) }
    [Console]::Error.WriteLine('StyleCop warnings dotnet format could not auto-fix remain above (SA0001 excluded) - fix them by hand, then retry.')
    exit 2
}

exit 0
