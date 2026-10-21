# Regenerates one vegetation model with its comparison renders, headless in the pinned Blender.
#
# Runs CSVM/data/vegetation_models/<Species>.py in Blender 5.2 with --background (never a window,
# never Godot), which writes CSVM/data/vegetation_models/<Chapter>/<Species>.glb.bin and, unless
# -NoRender, the model beside the original card into .scratch/vegetation/. The card is read from
# <data root>/extracted: $env:CSVM_DATA_ROOT, else this tree, else the main checkout (a worktree
# has no extracted/). Pipeline and conventions: docs/tooling.md, "Vegetation models".
#
# Pure ASCII on purpose (CLAUDE.md): PowerShell 5.1 mangles a BOM-less non-ASCII script.
#
# Usage:
#   .\MakeVegetationModel.ps1 -Species firtree1                  the generator's first chapter
#   .\MakeVegetationModel.ps1 -Species firtree1 -Chapter C1 -NoRender
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Species,
    [string]$Chapter,
    [switch]$NoRender,
    [string]$Blender = 'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe'
)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$script = Join-Path $root "CSVM\data\vegetation_models\$Species.py"
if (-not (Test-Path -LiteralPath $script)) { throw "No generator at $script" }
if (-not (Test-Path -LiteralPath $Blender)) { throw "Blender 5.2 not found at $Blender (pass -Blender)" }

$dataRoot = $env:CSVM_DATA_ROOT
if (-not $dataRoot) {
    $dataRoot = $root
    if (-not (Test-Path -LiteralPath (Join-Path $root 'extracted'))) {
        $common = git -C $root rev-parse --path-format=absolute --git-common-dir 2>$null
        if ($common) { $dataRoot = Split-Path -Parent $common }
    }
}

$scratch = Join-Path $root '.scratch\vegetation'
New-Item -ItemType Directory -Force -Path $scratch | Out-Null
$pyArgs = @('--data-root', $dataRoot)
if ($Chapter) { $pyArgs += @('--chapter', $Chapter) }
if (-not $NoRender) { $pyArgs += @('--render', $scratch) }
$log = Join-Path $scratch "$Species.log"

# Blender writes to stderr; under Stop, PowerShell 5.1 turns a redirected stderr line into a throw.
$ErrorActionPreference = 'Continue'
& $Blender --background --factory-startup --python-exit-code 1 --python $script -- @pyArgs *> $log
$code = $LASTEXITCODE
Get-Content -LiteralPath $log | Where-Object { $_ -match '^VEG ' }
if ($code -ne 0) {
    Get-Content -LiteralPath $log -Tail 30
    Write-Host "Blender failed (exit $code); full log: $log"
    exit $code
}
