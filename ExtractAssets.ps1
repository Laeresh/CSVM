<#
.SYNOPSIS
    Bulk-extracts every Crimson Skies ZBD archive into extracted/, mirroring the
    original CrimsonSkiesGame/ZBD folder structure (e.g. C1\IA1).

.DESCRIPTION
    Walks the retail game's ZBD tree, runs mech3ax `unzbd cs <mode>` on each file
    with the correct extraction mode for its type, and writes the output to the
    same relative path under extracted/ (keeping the basename, swapping the
    extension to .zip -- or .json for interp). Re-running only re-extracts files
    whose source is newer than the existing output, unless -Force is given.

    Mode mapping (game type `cs`):
        interp.zbd        -> interp    -> .json
        planes.zbd        -> gamez     -> .zip   (planes.zbd is a GameZ-format file)
        gamez.zbd         -> gamez     -> .zip
        soundsh/soundsl   -> sounds    -> .zip
        zrdr.zbd          -> reader    -> .zip
        rimage.zbd        -> textures  -> .zip
        texture.zbd       -> textures  -> .zip
        rtexture*.zbd     -> textures  -> .zip
        cam_anim.zbd      -> anim      -> .zip
        mis_anim.zbd      -> anim      -> .zip

    Anim archives (cam_anim/mis_anim) round-trip byte-identically in the fork since
    2026-07-21, are extracted like everything else, and are read by the Godot
    project's animation engine (CompiledAnim.cs -> AnimProgram.cs -> AnimRuntime.cs).

.PARAMETER Source
    Root of the game's ZBD tree. Default: CrimsonSkiesGame\ZBD next to this script.

.PARAMETER Unzbd
    Path to the unzbd.exe to extract with. Default: the fork build at
    tools\mech3ax\target\release\unzbd.exe (`cargo build --release` in tools\mech3ax).
    Pass the pinned tools\mech3ax-v0.6.1-...\unzbd.exe to fall back to the old binary --
    the Godot loaders read either extraction shape (see GameZ.cs), so that rollback
    needs no code change.

.PARAMETER Dest
    Output root. Default: extracted\ next to this script.

.PARAMETER Unzip
    Optional. After producing each .zip, also expand it into a sibling folder
    named after the archive (e.g. extracted\C1\gamez.zip -> extracted\C1\gamez\).
    The interp .json output has nothing to unzip and is left as-is.

.PARAMETER Force
    Re-extract every archive even when its output is already up to date.

.EXAMPLE
    .\ExtractAssets.ps1
    Extract all ZBDs into extracted\, mirroring the C1\IA1\... structure.

.EXAMPLE
    .\ExtractAssets.ps1 -Unzip
    Same, and also unpack every produced .zip into a sibling folder.
#>

[CmdletBinding()]
param(
    [string] $Source,
    [string] $Dest,
    [string] $Unzbd,
    [switch] $Unzip,
    [switch] $Force
)

$ErrorActionPreference = "Stop"

$RepoRoot = $PSScriptRoot
if (-not $Source) { $Source = Join-Path $RepoRoot "CrimsonSkiesGame\ZBD" }
if (-not $Dest)   { $Dest   = Join-Path $RepoRoot "extracted" }
# The fork (tools\mech3ax) is the extraction source of record since 2026-07-21: it is the
# only build with CS gamez/planes support that round-trips byte-identically (the pinned
# v0.6.1 binary leaves a 72-byte planes.zbd diff), and it also supports cam_anim/mis_anim.
$ForkUnzbd = Join-Path $RepoRoot "tools\mech3ax\target\release\unzbd.exe"
if (-not $Unzbd) { $Unzbd = $ForkUnzbd }
$UnzbdExe = $Unzbd

if (-not (Test-Path $UnzbdExe)) {
    if ($UnzbdExe -eq $ForkUnzbd) {
        throw "unzbd not found at $UnzbdExe -- build it with ``cargo build --release`` in tools\mech3ax, " +
              "or pass -Unzbd <path> (e.g. the pinned tools\mech3ax-v0.6.1-x86_64-pc-windows-msvc\unzbd.exe)."
    }
    throw "unzbd not found at $UnzbdExe"
}
if (-not (Test-Path $Source)) {
    throw "Source ZBD tree not found at $Source"
}

# Map a ZBD basename (no extension, lower-cased) to its unzbd mode + output
# extension. Returns $null for archives mech3ax can't extract for CS (skip them).
function Get-ExtractPlan([string] $BaseName) {
    switch -Regex ($BaseName.ToLowerInvariant()) {
        '^interp$'      { return @{ Mode = "interp";   Ext = ".json" } }
        '^planes$'      { return @{ Mode = "gamez";    Ext = ".zip"  } }
        '^gamez$'       { return @{ Mode = "gamez";    Ext = ".zip"  } }
        '^sounds[hl]$'  { return @{ Mode = "sounds";   Ext = ".zip"  } }
        '^zrdr$'        { return @{ Mode = "reader";   Ext = ".zip"  } }
        '^rimage$'      { return @{ Mode = "textures"; Ext = ".zip"  } }
        '^texture$'     { return @{ Mode = "textures"; Ext = ".zip"  } }
        '^rtexture\d+$' { return @{ Mode = "textures"; Ext = ".zip"  } }
        '^(cam_anim|mis_anim)$' { return @{ Mode = "anim"; Ext = ".zip"  } }
        default         { return "UNKNOWN" }
    }
}

$SourceFull = (Resolve-Path $Source).Path
$zbds = Get-ChildItem -Path $SourceFull -Recurse -File -Filter *.zbd | Sort-Object FullName

Write-Host "Extracting $($zbds.Count) ZBD file(s)" -ForegroundColor Cyan
Write-Host "  from: $SourceFull"
Write-Host "  to:   $Dest"
Write-Host "  with: $UnzbdExe"
if ($Unzip) { Write-Host "  (will also unzip produced archives)" }
Write-Host ""

$extracted = 0; $upToDate = 0; $skipped = 0; $unzipped = 0; $transformNotes = 0
$failures = New-Object System.Collections.Generic.List[string]
$unknowns = New-Object System.Collections.Generic.List[string]

foreach ($zbd in $zbds) {
    $baseName = [System.IO.Path]::GetFileNameWithoutExtension($zbd.Name)
    $rel = $zbd.FullName.Substring($SourceFull.Length).TrimStart('\', '/')
    $plan = Get-ExtractPlan $baseName

    if ($null -eq $plan) {
        Write-Host "  SKIP (unsupported) $rel" -ForegroundColor DarkGray
        $skipped++
        continue
    }
    if ($plan -eq "UNKNOWN") {
        Write-Host "  SKIP (unknown type) $rel" -ForegroundColor Yellow
        $unknowns.Add($rel)
        $skipped++
        continue
    }

    # Mirror the relative path under $Dest, swapping .zbd for the mode's extension.
    $outRel = [System.IO.Path]::ChangeExtension($rel, $plan.Ext)
    $outPath = Join-Path $Dest $outRel
    $outDir = Split-Path -Parent $outPath
    if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir -Force | Out-Null }

    $upToDateAlready = (Test-Path $outPath) -and
                       ((Get-Item $outPath).LastWriteTime -ge $zbd.LastWriteTime)

    if ($upToDateAlready -and -not $Force) {
        Write-Host "  ok   $outRel (up to date)" -ForegroundColor DarkGray
        $upToDate++
    }
    else {
        Write-Host "  ->   $outRel  [$($plan.Mode)]" -ForegroundColor Green
        # unzbd writes diagnostics to stderr, and $ErrorActionPreference = "Stop" would
        # turn any of them into a terminating error (PowerShell 5.1 wraps a native exe's
        # stderr in ErrorRecords). Capture instead, and judge by the exit code.
        $prevEap = $ErrorActionPreference
        $ErrorActionPreference = "Continue"
        $output = & $UnzbdExe cs $plan.Mode $zbd.FullName $outPath 2>&1
        $exit = $LASTEXITCODE
        $ErrorActionPreference = $prevEap

        $stderrLines = @($output | Where-Object { $_ -is [System.Management.Automation.ErrorRecord] })
        # "object3d transform fail" is upstream mech3ax noting that recomposing a node's
        # transform from its euler angles doesn't reproduce the stored matrix bit-for-bit.
        # It is informational, not a failure: the original matrix is preserved verbatim
        # (that is what makes the gamez round-trip byte-identical), and the Godot loader
        # reads it in preference to the angles. Counted, not printed -- a few hundred
        # across a full run would bury real errors.
        $benign = @($stderrLines | Where-Object { "$_" -match "object3d transform fail" })
        $unexpected = @($stderrLines | Where-Object { "$_" -notmatch "object3d transform fail" })

        if ($exit -ne 0) {
            Write-Host "       FAILED (unzbd exit $exit)" -ForegroundColor Red
            $stderrLines | ForEach-Object { Write-Host "         $_" -ForegroundColor Red }
            $failures.Add("$rel (exit $exit)")
            continue
        }
        foreach ($line in $unexpected) { Write-Host "       $line" -ForegroundColor Yellow }
        if ($benign.Count -gt 0) {
            Write-Host "       ($($benign.Count) transform-precision notes)" -ForegroundColor DarkGray
            $transformNotes += $benign.Count
        }
        $extracted++
    }

    # Optional: expand the produced .zip into a sibling folder (skip .json).
    if ($Unzip -and $plan.Ext -eq ".zip" -and (Test-Path $outPath)) {
        $expandDir = Join-Path $outDir ([System.IO.Path]::GetFileNameWithoutExtension($outPath))
        if ((Test-Path $expandDir) -and -not $Force) {
            $srcNewer = (Get-Item $outPath).LastWriteTime -gt (Get-Item $expandDir).LastWriteTime
            if (-not $srcNewer) { continue }
        }
        if (Test-Path $expandDir) { Remove-Item $expandDir -Recurse -Force }
        Expand-Archive -Path $outPath -DestinationPath $expandDir -Force
        $unzipped++
    }
}

Write-Host ""
Write-Host "Done." -ForegroundColor Cyan
Write-Host "  extracted:  $extracted"
Write-Host "  up to date: $upToDate"
Write-Host "  skipped:    $skipped"
if ($Unzip)              { Write-Host "  unzipped:   $unzipped" }
if ($transformNotes -gt 0) {
    Write-Host "  transform-precision notes: $transformNotes (informational -- see the comment in this script)" -ForegroundColor DarkGray
}
if ($unknowns.Count -gt 0) {
    Write-Host "  UNKNOWN types (no mode mapped -- check the script):" -ForegroundColor Yellow
    $unknowns | ForEach-Object { Write-Host "    $_" -ForegroundColor Yellow }
}
if ($failures.Count -gt 0) {
    Write-Host "  FAILURES:" -ForegroundColor Red
    $failures | ForEach-Object { Write-Host "    $_" -ForegroundColor Red }
    exit 1
}
