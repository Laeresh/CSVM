#!/usr/bin/env pwsh
# Godot .uid sidecar checker for the CSVM/ project folder.
#
# Godot gives every .cs and .gdshaderinc under res:// a <file>.uid sidecar holding the uid the
# engine references it by, and it writes one only when the editor or an import runs. A file added
# by hand or by an agent has none until then, and two branches that each import later mint
# DIFFERENT uids for the same file, which then conflict at the merge. So a file without its
# sidecar is caught here, in the tree that adds it, before the commit.
#
# The fix the failure prints is a headless import of that tree. When the file already landed on
# another branch, take that branch's sidecar instead of keeping a freshly minted one.
#
# Pure ASCII on purpose (PROJECT_CONTEXT.md). Files are READ only; nothing here writes.
#
# Usage:
#   ./CheckUidSidecars.ps1                 this tree
#   ./CheckUidSidecars.ps1 -Root <path>    another worktree
[CmdletBinding()]
param([string]$Root)

if (-not $Root) { $Root = $PSScriptRoot }
if (-not $Root) { $Root = (Get-Location).Path }

# Tracked plus untracked-not-ignored: the files a commit from this tree could carry. A tracked file
# deleted in the working tree is going away and needs no sidecar.
$files = @(git -C $Root ls-files --cached --others --exclude-standard -- 'CSVM/*.cs' 'CSVM/*.gdshaderinc' 2>$null |
    Sort-Object -Unique)
$missing = @($files | Where-Object {
    (Test-Path -LiteralPath (Join-Path $Root $_) -PathType Leaf) -and
    -not (Test-Path -LiteralPath (Join-Path $Root ($_ + '.uid')) -PathType Leaf)
})

if ($missing.Count -eq 0) {
    Write-Output 'every .cs and .gdshaderinc under CSVM/ has its .uid'
    exit 0
}
foreach ($m in $missing) { Write-Output ($m + '  has no .uid sidecar') }
Write-Output ''
# tools/ is git-ignored and lives only in the main checkout, which owns the common .git folder.
$common = git -C $Root rev-parse --path-format=absolute --git-common-dir 2>$null
$main = if ($common) { Split-Path -Parent ([string]$common) } else { $Root }
$godot = Join-Path $main 'tools/godot/Godot_v4.7-stable_mono_win64/Godot_v4.7-stable_mono_win64_console.exe'
Write-Output ('{0} files without a .uid. Generate them with a headless import of this tree:' -f $missing.Count)
Write-Output ('  & "' + $godot + '" --path "' + (Join-Path $Root 'CSVM') + '" --headless --import')
Write-Output 'then commit each <file>.uid beside its file.'
exit 1
