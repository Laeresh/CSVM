#!/usr/bin/env pwsh
# Mojibake tripwire: finds text that was UTF-8, got read as ANSI (CP1252), and was re-saved.
#
# The corruption is reversible, and that is what makes it detectable without guessing. A UTF-8
# byte sequence read as CP1252 becomes one char per byte: U+26A0 is E2 9A A0, which surfaces as
# "a-circumflex, s-caron, no-break space". Mapping those chars back to bytes and decoding them
# as strict UTF-8 recovers the character that was lost. A run that does NOT decode, or decodes
# to something outside the ranges this repo writes in, is a valid pair of characters and is left
# alone: multiplication-sign followed by en-dash maps to D7 96, which is Hebrew zayin, so it is
# not corruption.
#
# The round-trip is what lets the lead set be every UTF-8 lead byte rather than the three that a
# pattern-only check can afford. A pattern-only check has to stay narrow to avoid false alarms,
# and a narrow lead set cannot see a corrupted Greek letter (lead CE), superscript (E1) or
# emoji (F0) at all.
#
# Scope is the whole tree, not the changed set, so content that arrived by merge, pull or branch
# switch is caught at the next commit. Enumeration is git's, which means ignored paths are
# unreachable: nothing here can walk videodata/ or OriginalScreenshots/.
#
# Pure ASCII on purpose (PROJECT_CONTEXT.md): PowerShell 5.1 mangles a BOM-less non-ASCII script
# before it runs, so every character above 126 is built from its code point. Files are READ only.
#
# Exit code 1 when anything is corrupt, so a caller can gate a commit on it.
#
# Usage:
#   ./CheckEncoding.ps1                    this tree
#   ./CheckEncoding.ps1 -Root <path>       another worktree
#   ./CheckEncoding.ps1 -Quiet             exit code only
[CmdletBinding()]
param(
    [string]$Root,
    [switch]$Quiet
)

if (-not $Root) { $Root = $PSScriptRoot }
if (-not $Root) { $Root = (Get-Location).Path }

$textExtensions = '\.(cs|md|json|ps1|gdshader|gdshaderinc|txt|csproj|sln|tscn|tres|cfg|godot|yml|yaml)$'

# CP1252's 0x80-0x9F block. Every other byte 0xA0-0xFF maps to the same-numbered code point, and
# 0x81/0x8D/0x8F/0x90/0x9D are undefined, so a char cannot have come from them.
$specials = @{
    0x20AC = 0x80; 0x201A = 0x82; 0x0192 = 0x83; 0x201E = 0x84; 0x2026 = 0x85
    0x2020 = 0x86; 0x2021 = 0x87; 0x02C6 = 0x88; 0x2030 = 0x89; 0x0160 = 0x8A
    0x2039 = 0x8B; 0x0152 = 0x8C; 0x017D = 0x8E; 0x2018 = 0x91; 0x2019 = 0x92
    0x201C = 0x93; 0x201D = 0x94; 0x2022 = 0x95; 0x2013 = 0x96; 0x2014 = 0x97
    0x02DC = 0x98; 0x2122 = 0x99; 0x0161 = 0x9A; 0x203A = 0x9B; 0x0153 = 0x9C
    0x017E = 0x9E; 0x0178 = 0x9F
}

function ConvertTo-Cp1252Byte {
    param([char]$Char)
    $c = [int]$Char
    if ($c -ge 0xA0 -and $c -le 0xFF) { return $c }
    if ($specials.ContainsKey($c)) { return $specials[$c] }
    return -1
}

# Ranges this repo writes in. A decode landing outside them is a coincidence, not a lost
# character: the point is to separate "these bytes happen to form valid UTF-8" from "these bytes
# ARE a character somebody typed".
$plausible = @(
    @(0x00A0, 0x00FF),   # Latin-1 punctuation and symbols: degree, plus-minus, superscripts
    @(0x0100, 0x024F),   # Latin extended
    @(0x0370, 0x03FF),   # Greek, used throughout the flight-model prose
    @(0x1D00, 0x1D7F),   # phonetic/modifier letters, e.g. the superscript T
    @(0x2000, 0x2BFF),   # punctuation, arrows, maths, box drawing, dingbats
    @(0xFE0F, 0xFE0F),   # variation selector, trails an emoji
    @(0x1D400, 0x1D7FF), # mathematical alphanumerics
    @(0x1F000, 0x1FAFF)  # emoji
)

function Test-Plausible {
    param([int]$CodePoint)
    foreach ($r in $plausible) {
        if ($CodePoint -ge $r[0] -and $CodePoint -le $r[1]) { return $true }
    }
    return $false
}

$strictUtf8 = New-Object System.Text.UTF8Encoding($false, $true)

# Does the run of $Length chars starting at $Index spell a character that was lost? Returns the
# recovered character's code point, or -1.
function Get-RecoveredCodePoint {
    param([string]$Text, [int]$Index, [int]$Length)
    if ($Index + $Length -gt $Text.Length) { return -1 }
    $bytes = New-Object byte[] $Length
    for ($k = 0; $k -lt $Length; $k++) {
        $b = ConvertTo-Cp1252Byte -Char $Text[$Index + $k]
        if ($b -lt 0) { return -1 }
        $bytes[$k] = [byte]$b
    }
    try { $decoded = $strictUtf8.GetString($bytes) } catch { return -1 }
    # A shorter prefix that also decodes would have matched first; only a whole-run decode counts.
    if ([string]::IsNullOrEmpty($decoded)) { return -1 }
    $cp = [char]::ConvertToUtf32($decoded, 0)
    if ([char]::ConvertFromUtf32($cp).Length -ne $decoded.Length) { return -1 }
    if (-not (Test-Plausible -CodePoint $cp)) { return -1 }
    return $cp
}

# Prefilter. Any UTF-8 lead byte (C2-F4) as a CP1252 char, followed by any continuation byte
# (80-BF) as a CP1252 char. Cheap, and the round-trip decides.
$continuationChars = ('{0}-{1}' -f [char]0xA0, [char]0xBF) +
    (($specials.Keys | Sort-Object | ForEach-Object { [string][char]$_ }) -join '')
$candidateRx = [regex]('[' + [char]0xC2 + '-' + [char]0xF4 + '][' + $continuationChars + ']')

$files = @(git -C $Root ls-files) + @(git -C $Root ls-files --others --exclude-standard)
$findings = @()
foreach ($f in ($files | Sort-Object -Unique)) {
    if ($f -notmatch $textExtensions) { continue }
    $p = Join-Path $Root $f
    if (-not (Test-Path -LiteralPath $p -PathType Leaf)) { continue }
    if ((Get-Item -LiteralPath $p).Length -gt 2MB) { continue }
    try { $text = [IO.File]::ReadAllText($p) } catch { continue }

    foreach ($m in $candidateRx.Matches($text)) {
        $cp = -1
        foreach ($len in 2, 3, 4) {
            $cp = Get-RecoveredCodePoint -Text $text -Index $m.Index -Length $len
            if ($cp -ge 0) { break }
        }
        if ($cp -lt 0) { continue }
        $line = ($text.Substring(0, $m.Index) -split [string][char]10).Count
        $findings += [pscustomobject]@{
            File = $f
            Line = $line
            Lost = ('U+{0:X4}' -f $cp)
        }
        break   # one report per file is enough to stop the commit
    }
}

if ($findings.Count -eq 0) {
    if (-not $Quiet) { Write-Output 'no mojibake' }
    exit 0
}

if (-not $Quiet) {
    foreach ($x in $findings) {
        Write-Output ('Mojibake (UTF-8 read as ANSI and re-saved): {0}:{1} - was {2}' -f
            $x.File, $x.Line, $x.Lost)
    }
    Write-Output ''
    Write-Output 'Cause: a PowerShell file write without UTF-8 - a Get-Content/Set-Content round-trip'
    Write-Output 'without -Encoding utf8, or a BOM-less file read as ANSI. Repair the characters before'
    Write-Output 'committing; edit repo text with the Read/Edit/Write tools, and pass -Encoding utf8 on'
    Write-Output 'both read and write whenever PowerShell must touch a repo text file.'
}
exit 1
