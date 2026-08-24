#!/usr/bin/env pwsh
# Comment-length cap checker for CSVM/src and CSVM.Tests.
#
# The caps are PROJECT_CONTEXT.md's coding conventions:
#   /// on a type          12 lines
#   /// on a member         6
#   // above a declaration  6
#   // above a statement    3
#
# A block over cap is a comment that has outgrown its subject: the fix is almost never to
# reflow it, but to move the part that is really a decode into docs/ and keep the prohibition
# on the member it binds. Exit code 1 when anything is over, so a PreToolUse hook can gate a
# commit on it.
#
# Pure ASCII on purpose (PROJECT_CONTEXT.md): PowerShell 5.1 mangles a BOM-less non-ASCII
# script before it runs. Files are READ only; nothing here writes.
#
# Usage:
#   ./CheckCommentCaps.ps1                 the whole scope
#   ./CheckCommentCaps.ps1 -Summary        one line per file, worst first
#   ./CheckCommentCaps.ps1 a.cs b.cs       just these files
[CmdletBinding()]
param(
    [switch]$Summary,
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$Path
)

$caps = @{ type = 12; member = 6; decl = 6; stmt = 3 }
$labels = @{
    type   = '/// on a type'
    member = '/// on a member'
    decl   = '// above a declaration'
    stmt   = '// above a statement'
}

# $PSScriptRoot names the worktree this file lives in regardless of the caller's cwd; a
# git-rev-parse-on-cwd root scanned whatever worktree the caller happened to be sitting in.
$root = $PSScriptRoot
if (-not $root) { $root = git rev-parse --show-toplevel 2>$null }
if (-not $root) { $root = (Get-Location).Path }

function Get-Scope {
    param([string]$Root)
    $dirs = @((Join-Path $Root 'CSVM/src'), (Join-Path $Root 'CSVM.Tests'))
    $files = @()
    foreach ($d in $dirs) {
        if (Test-Path -LiteralPath $d) {
            $files += @(Get-ChildItem -LiteralPath $d -Filter *.cs -Recurse -File)
        }
    }
    # Generated trees are not ours.
    $files | Where-Object { $_.FullName -notmatch '\\(obj|bin|\.godot)\\' } | ForEach-Object { $_.FullName }
}

# Which cap applies to the block ending at $end, and what it is attached to.
function Get-BlockKind {
    param([string[]]$Lines, [int]$End, [bool]$IsXml)
    $k = $End
    while ($k -lt $Lines.Count -and ($Lines[$k].Trim() -eq '' -or $Lines[$k] -match '^\s*\[')) { $k++ }
    $decl = if ($k -lt $Lines.Count) { $Lines[$k].Trim() } else { '' }
    if ($decl -match '\b(class|struct|enum|interface|record)\b' -and -not $decl.EndsWith(';')) {
        return @{ Kind = 'type'; Decl = $decl }
    }
    if ($IsXml) { return @{ Kind = 'member'; Decl = $decl } }
    if ($decl -match '^(private|public|internal|protected|const|static|readonly)') {
        return @{ Kind = 'decl'; Decl = $decl }
    }
    return @{ Kind = 'stmt'; Decl = $decl }
}

function Get-OverCap {
    param([string]$File)
    $lines = ([IO.File]::ReadAllText($File) -replace "`r`n", "`n") -split "`n"
    $out = @()
    $i = 0
    while ($i -lt $lines.Count) {
        $xml = $lines[$i] -match '^\s*///'
        $plain = $lines[$i] -match '^\s*//([^/]|$)'
        if (-not ($xml -or $plain)) { $i++; continue }
        $pattern = if ($xml) { '^\s*///' } else { '^\s*//([^/]|$)' }
        $j = $i
        while ($j -lt $lines.Count -and $lines[$j] -match $pattern) { $j++ }
        $info = Get-BlockKind -Lines $lines -End $j -IsXml $xml
        $n = $j - $i
        if ($n -gt $caps[$info.Kind]) {
            $out += [pscustomobject]@{
                Line = $i + 1; Length = $n; Kind = $info.Kind; Decl = $info.Decl
            }
        }
        $i = $j
    }
    $out
}

$targets = if ($Path) { $Path } else { Get-Scope -Root $root }
$rows = @()
$totalBlocks = 0
$totalExcess = 0
foreach ($f in $targets) {
    if (-not (Test-Path -LiteralPath $f -PathType Leaf)) { continue }
    # Resolve-Path (not the raw arg) goes to ReadAllText: it honors PowerShell's $PWD, while
    # a relative path handed straight to a .NET file API follows the process's own current
    # directory, which Set-Location does not keep in step with $PWD in a hosted session.
    $full = (Resolve-Path -LiteralPath $f).Path
    $bad = @(Get-OverCap -File $full)
    if ($bad.Count -eq 0) { continue }
    $rel = $full.Replace($root + '\', '')
    $excess = ($bad | ForEach-Object { $_.Length - $caps[$_.Kind] } | Measure-Object -Sum).Sum
    $totalBlocks += $bad.Count
    $totalExcess += $excess
    $rows += [pscustomobject]@{ Excess = $excess; Blocks = $bad.Count; File = $rel }
    if (-not $Summary) {
        foreach ($b in $bad) {
            $shortDecl = if ($b.Decl.Length -gt 60) { $b.Decl.Substring(0, 60) } else { $b.Decl }
            Write-Host ('{0}:{1}  {2} lines, cap {3} ({4})  {5}' -f
                $rel, $b.Line, $b.Length, $caps[$b.Kind], $labels[$b.Kind], $shortDecl)
        }
    }
}

if ($Summary) {
    foreach ($r in ($rows | Sort-Object Excess -Descending)) {
        Write-Host ('{0,6} excess  {1,4} blocks  {2}' -f $r.Excess, $r.Blocks, $r.File)
    }
}

if ($totalBlocks -gt 0) {
    Write-Host ''
    Write-Host ('{0} blocks over cap, {1} excess lines, {2} files' -f
        $totalBlocks, $totalExcess, $rows.Count)
    exit 1
}
Write-Host 'all comment blocks within cap'
exit 0
