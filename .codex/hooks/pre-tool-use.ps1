#!/usr/bin/env pwsh
# Ordered PreToolUse safeguards converted from .claude/settings.json.

$ErrorActionPreference = 'Stop'
$raw = [Console]::In.ReadToEnd()
if (-not $raw) { exit 0 }
try { $j = $raw | ConvertFrom-Json } catch { exit 0 }
$command = [string]$j.tool_input.command
if (-not $command) { exit 0 }

# Codex has one canonical shell matcher (Bash), including Windows shell commands.
# The Claude Bash-vs-PowerShell guard is not copied because Codex does not expose that distinction.
if ($command -match 'RunTests\.ps1|dotnet\s+test|git\s+commit') {
    $repo = git rev-parse --show-toplevel 2>$null
    if ($repo) {
        $proj = Join-Path $repo 'CSVM/CSVM.csproj'
        dotnet format $proj --verbosity quiet
        $buildOutput = dotnet build $proj --verbosity quiet --nologo -t:Rebuild 2>&1
        $remaining = $buildOutput | Select-String -Pattern 'SA\d{4}' | Where-Object { $_.Line -notmatch 'SA0001' }
        if ($remaining) {
            $remaining | ForEach-Object { [Console]::Error.WriteLine($_.Line) }
            [Console]::Error.WriteLine('StyleCop warnings dotnet format could not auto-fix remain above (SA0001 excluded) - fix them by hand, then retry.')
            exit 2
        }
    }
}
if ($command -notmatch 'git\s+commit') { exit 0 }
$repo = git rev-parse --show-toplevel 2>$null
if (-not $repo) { exit 0 }

$defs = @()
$backlog = Join-Path $repo 'backlog.md'
if (Test-Path -LiteralPath $backlog) {
    $defs += @(Select-String -Path $backlog -Pattern '^\s*-\s+`(BL-\d+)`' -AllMatches | ForEach-Object { $_.Matches } | ForEach-Object { $_.Groups[1].Value })
}
$playtest = Join-Path $repo 'playtest.md'
if (Test-Path -LiteralPath $playtest) {
    $defs += @(Select-String -Path $playtest -Pattern '^\s*(?:-\s+|\|\s*)`((?:PT|CAP)-\d+)`' -AllMatches | ForEach-Object { $_.Matches } | ForEach-Object { $_.Groups[1].Value })
}
$dupes = $defs | Group-Object | Where-Object { $_.Count -gt 1 }
if ($dupes) {
    $dupes | ForEach-Object { [Console]::Error.WriteLine('Duplicate item ID defined more than once: ' + $_.Name) }
    [Console]::Error.WriteLine('backlog.md/playtest.md define the same ID twice - mint a fresh ID with ./New-ItemId.ps1 and renumber the later mint before committing.')
    exit 2
}

$changed = @(git diff --name-only HEAD --diff-filter=ACM) + @(git ls-files --others --exclude-standard)
$extensions = '\.(cs|md|json|ps1|gdshader|gdshaderinc|txt|csproj|sln|tscn|tres|cfg|godot|yml|yaml)$'
$hi = 0x0152,0x0153,0x0160,0x0161,0x0178,0x017D,0x017E,0x0192,0x02C6,0x02DC,0x2013,0x2014,0x2018,0x2019,0x201A,0x201C,0x201D,0x201E,0x2020,0x2021,0x2022,0x2026,0x2030,0x2039,0x203A,0x20AC,0x2122
$follow = ('{0}-{1}' -f [char]0x80, [char]0xBF) + (($hi | ForEach-Object { [string][char]$_ }) -join '')
$mojibake = [regex]('[' + [char]0xC2 + [char]0xC3 + [char]0xE2 + '][' + $follow + ']')
$bad = @()
foreach ($file in ($changed | Sort-Object -Unique)) {
    if ($file -notmatch $extensions) { continue }
    $path = Join-Path $repo $file
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { continue }
    try { $text = [IO.File]::ReadAllText($path) } catch { continue }
    $match = $mojibake.Match($text)
    if ($match.Success) {
        $line = ($text.Substring(0, $match.Index) -split [string][char]10).Count
        $bad += ($file + ':' + $line)
    }
}
if ($bad) {
    $bad | ForEach-Object { [Console]::Error.WriteLine('Mojibake about to be committed: ' + $_) }
    [Console]::Error.WriteLine('Repair the corrupted characters before committing; use UTF-8 for repository text files.')
    exit 2
}

$manifest = Join-Path $repo 'analysis/goldens/manifest.json'
if (Test-Path -LiteralPath $manifest -PathType Leaf) {
    try { $m = [IO.File]::ReadAllText($manifest) | ConvertFrom-Json }
    catch {
        [Console]::Error.WriteLine('analysis/goldens/manifest.json does not parse as JSON - fix it before committing.')
        exit 2
    }
    $bad = @()
    foreach ($shot in $m.shots) {
        $exercise = [string]$shot.exercises
        if ($exercise.Length -gt 250) { $bad += ($shot.name + ': exercises is ' + $exercise.Length + ' chars, cap is 250') }
        if ($exercise -match '(BL|PT|CAP)-\d+|PLAN-|[Aa]lso exercises|\d{4}-\d{2}-\d{2}') {
            $bad += ($shot.name + ': exercises carries history or an item id -- ' + $Matches[0])
        }
    }
    if ($bad) {
        $bad | ForEach-Object { [Console]::Error.WriteLine('BLOCKED: ' + $_) }
        [Console]::Error.WriteLine('analysis/goldens/manifest.json: rewrite exercises as a current, one-sentence description under 250 chars.')
        exit 2
    }
}
exit 0
